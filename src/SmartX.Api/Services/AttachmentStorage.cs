using SmartX.Api.Contracts;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

public sealed class AttachmentStorage(IWebHostEnvironment env)
{
    public async Task<SensorAttachment> SaveAsync(Guid sensorId, IFormFile file, CancellationToken ct)
    {
        var root = Path.Combine(env.ContentRootPath, "App_Data", "uploads", sensorId.ToString("N"));
        Directory.CreateDirectory(root);
        var safeName = Path.GetFileName(file.FileName);
        var stored = Path.Combine(root, $"{Guid.NewGuid():N}_{safeName}");
        await using var stream = File.Create(stored);
        await file.CopyToAsync(stream, ct);
        return new SensorAttachment
        {
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            StoredPath = stored,
            ByteSize = file.Length
        };
    }

    public (string Path, string ContentType, string DownloadName)? Resolve(SensorProfile sensor, Guid attachmentId)
    {
        var attachment = sensor.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null || !File.Exists(attachment.StoredPath))
        {
            return null;
        }

        return (attachment.StoredPath, attachment.ContentType, attachment.FileName);
    }
}
