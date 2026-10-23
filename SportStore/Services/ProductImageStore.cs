namespace SportStore.Services;

public class ProductImageStore(string directory)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/products/";

    public async Task<string> SaveAsync(IFormFile photo)
    {
        if (photo.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Выберите фото размером до 5 МБ.");

        await using var input = photo.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var extension = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png"
            : read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255 ? ".jpg"
            : read == 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? ".webp"
            : throw new InvalidDataException("Поддерживаются только изображения JPG, PNG и WebP.");

        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(directory, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await output.WriteAsync(header.AsMemory(0, read));
            await input.CopyToAsync(output);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
        return UrlPrefix + name;
    }

    public void Delete(string? imagePath)
    {
        if (imagePath?.StartsWith(UrlPrefix, StringComparison.Ordinal) != true) return;
        var name = imagePath[UrlPrefix.Length..];
        if (name != Path.GetFileName(name)) return;
        File.Delete(Path.Combine(directory, name));
    }
}
