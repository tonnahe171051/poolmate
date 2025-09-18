namespace PoolMate.Api.Integrations.Cloudinary36
{
    public record SignUploadResult(
    string CloudName, string ApiKey, string Signature, string Timestamp,
    string Folder, string PublicId, string UploadPreset);

    public interface ICloudinaryService
    {
        SignUploadResult SignAvatarUpload(string userId);
        Task<bool> DeleteAsync(string publicId);
    }
}
