using System.Globalization;
using System.Net.Http.Headers;
using AgentBridge.Application.Models;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Auxiliary;

/// <summary>Фиксирует параметры и файлы Images API до первого await, создаёт fresh transport content.</summary>
internal static class Sub2ApiImageRequestFactory
{
    /// <summary>Проверяет входную границу без guesses о поддерживаемых сервером enum/model IDs.</summary>
    internal static LibraryHttpRequestOptions? Create(ModelImageRequest request, string endpoint, string key, bool edit)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(request.Prompt) || request.Count <= 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Size) || string.IsNullOrWhiteSpace(request.Quality)
            || string.IsNullOrWhiteSpace(request.Background) || string.IsNullOrWhiteSpace(request.OutputFormat))
        {
            return null;
        }

        Dictionary<string, object> fields = new()
        {
            ["model"] = request.Model, ["prompt"] = request.Prompt, ["n"] = request.Count,
            ["size"] = request.Size, ["quality"] = request.Quality, ["background"] = request.Background,
            ["output_format"] = request.OutputFormat, ["stream"] = false
        };
        Dictionary<string, string> headers = new() { ["Authorization"] = "Bearer " + key };
        if (!edit)
        {
            return new() { Method = HttpMethod.Post, Url = endpoint, Headers = headers, Body = fields };
        }

        if (request is not ModelImageEditRequest edits || edits.Images is null || edits.Images.Count == 0)
        {
            return null;
        }

        ModelFileUpload[] inputs = edits.Images.ToArray();
        List<ModelFileUpload> snapshot = new(inputs.Length);
        foreach (ModelFileUpload image in inputs)
        {
            if (image is null || image.Bytes is null || image.Bytes.Length == 0 || !ValidFileName(image.FileName))
            {
                return null;
            }

            string mimeType = image.MimeType ?? "application/octet-stream";
            if (mimeType.Any(char.IsControl) || !MediaTypeHeaderValue.TryParse(mimeType, out _))
            {
                return null;
            }

            snapshot.Add(new() { FileName = image.FileName, MimeType = mimeType, Bytes = image.Bytes.ToArray() });
        }

        return new()
        {
            Method = HttpMethod.Post, Url = endpoint, Headers = headers,
            ContentFactory = () => Multipart(fields, snapshot)
        };
    }

    /// <summary>Не допускает пути и управляющие символы в имени multipart-файла.</summary>
    private static bool ValidFileName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl)
            && name.IndexOfAny(['/', '\\', ':']) < 0 && name is not ("." or "..");
    }

    /// <summary>Передаёт владение всем content HttpClientLibrary; при ошибке построения освобождает части.</summary>
    private static HttpContent Multipart(Dictionary<string, object> fields, List<ModelFileUpload> images)
    {
        MultipartFormDataContent content = new();
        try
        {
            foreach (KeyValuePair<string, object> field in fields)
            {
                string value = Convert.ToString(field.Value, CultureInfo.InvariantCulture)!;
                if (field.Key == "stream")
                {
                    value = "false";
                }

                content.Add(new StringContent(value), field.Key);
            }

            foreach (ModelFileUpload image in images)
            {
                ByteArrayContent file = new(image.Bytes);
                file.Headers.ContentType = MediaTypeHeaderValue.Parse(image.MimeType!);
                content.Add(file, "image[]", image.FileName);
            }

            return content;
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }
}
