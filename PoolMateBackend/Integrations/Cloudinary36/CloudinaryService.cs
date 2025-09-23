using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using CloudinaryDotNet; 


namespace PoolMate.Api.Integrations.Cloudinary36
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloud;
        private readonly CloudinaryOptions _opt;
        private readonly IConfiguration _cfg;

        public CloudinaryService(Cloudinary cloud,
                                 IOptions<CloudinaryOptions> opt,
                                 IConfiguration cfg)
        {
            _cloud = cloud;
            _opt = opt.Value;
            _cfg = cfg;
        }

        // public_id for overrite: users/{userId}/avatar
        public SignUploadResult SignAvatarUpload(string userId)
        {
            var folder = _opt.Folder ?? "users/images";
            var preset = _opt.UploadPreset ?? "poolmate_image";
            var publicId = $"users/{userId}/avatar";

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var toSign = new SortedDictionary<string, object>
        {
            { "timestamp", ts },
            { "folder", folder },
            { "public_id", publicId },
            { "overwrite", "true" },
            { "upload_preset", preset }
        };

            var sig = _cloud.Api.SignParameters(toSign);

            return new SignUploadResult(
                CloudName: _cfg["Cloudinary:CloudName"]!,
                ApiKey: _cfg["Cloudinary:ApiKey"]!,
                Signature: sig,
                Timestamp: ts,
                Folder: folder,
                PublicId: publicId,
                UploadPreset: preset
            );
        }

        // public_id cho post image: posts/{userId}/{postId}
        public SignUploadResult SignPostImageUpload(string userId, string postId)
        {
            var folder = _opt.Folder ?? "posts/images";
            var preset = _opt.UploadPreset ?? "poolmate_image";
            var publicId = $"posts/{userId}/{postId}";

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var toSign = new SortedDictionary<string, object>
            {
                { "timestamp", ts },
                { "folder", folder },
                { "public_id", publicId },
                { "overwrite", "true" },
                { "upload_preset", preset }
            };

            var sig = _cloud.Api.SignParameters(toSign);

            return new SignUploadResult(
                CloudName: _cfg["Cloudinary:CloudName"]!,
                ApiKey: _cfg["Cloudinary:ApiKey"]!,
                Signature: sig,
                Timestamp: ts,
                Folder: folder,
                PublicId: publicId,
                UploadPreset: preset
            );
        }
        public async Task<bool> DeleteAsync(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId)) return true;
            var res = await _cloud.DestroyAsync(new DeletionParams(publicId));
            return res.StatusCode == System.Net.HttpStatusCode.OK;
        }
    }
}
