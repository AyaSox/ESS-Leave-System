namespace ESSLeaveSystem.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderPath = "documents");
        Task<string> UploadProfilePictureAsync(IFormFile file, int employeeId);
        Task<bool> DeleteFileAsync(string filePath);
        bool IsValidFileType(IFormFile file, string[] allowedExtensions);
        bool IsValidFileSize(IFormFile file, long maxSizeInBytes = 10485760); // 10MB default
    }

    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly IConfiguration _configuration;

        public FileUploadService(
            IWebHostEnvironment environment, 
            ILogger<FileUploadService> logger,
            IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderPath = "documents")
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File is empty or null");

                // Validate file type
                var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions")
                    .Get<string[]>() ?? new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                
                if (!IsValidFileType(file, allowedExtensions))
                    throw new ArgumentException($"File type not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");

                // Validate file size
                var maxSizeInBytes = _configuration.GetValue<long>("FileUpload:MaxSizeInBytes", 10485760); // 10MB default
                if (!IsValidFileSize(file, maxSizeInBytes))
                    throw new ArgumentException($"File size exceeds maximum allowed size of {maxSizeInBytes / 1024 / 1024}MB");

                // Create unique filename
                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

                // Create directory if it doesn't exist
                var uploadPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, folderPath);
                Directory.CreateDirectory(uploadPath);

                // Full file path
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return relative path for storage in database
                var relativePath = $"/{folderPath}/{uniqueFileName}";
                
                _logger.LogInformation("File uploaded successfully: {FileName} -> {RelativePath}", 
                    file.FileName, relativePath);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", file?.FileName);
                throw;
            }
        }

        public async Task<string> UploadProfilePictureAsync(IFormFile file, int employeeId)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File is empty or null");

                // Validate file type - only images
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                if (!IsValidFileType(file, allowedExtensions))
                    throw new ArgumentException($"Only image files are allowed: {string.Join(", ", allowedExtensions)}");

                // Validate file size (max 5MB for profile pictures)
                var maxSizeInBytes = 5 * 1024 * 1024; // 5MB
                if (!IsValidFileSize(file, maxSizeInBytes))
                    throw new ArgumentException($"File size exceeds maximum allowed size of {maxSizeInBytes / 1024 / 1024}MB");

                // Create unique filename with employee ID
                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"profile_{employeeId}_{Guid.NewGuid()}{fileExtension}";

                // Create directory if it doesn't exist
                var uploadPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "images", "profiles");
                Directory.CreateDirectory(uploadPath);

                // Full file path
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return relative path for storage in database
                var relativePath = $"/images/profiles/{uniqueFileName}";
                
                _logger.LogInformation("Profile picture uploaded successfully: {FileName} -> {RelativePath} for Employee {EmployeeId}", 
                    file.FileName, relativePath, employeeId);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture: {FileName} for Employee {EmployeeId}", file?.FileName, employeeId);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return false;

                // Convert relative path to absolute path
                var absolutePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, 
                    filePath.TrimStart('/'));

                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                    _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                    return true;
                }

                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                return false;
            }
        }

        public bool IsValidFileType(IFormFile file, string[] allowedExtensions)
        {
            if (file == null) return false;

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(fileExtension);
        }

        public bool IsValidFileSize(IFormFile file, long maxSizeInBytes = 10485760)
        {
            return file != null && file.Length <= maxSizeInBytes;
        }
    }
}