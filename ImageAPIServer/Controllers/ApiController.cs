using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using CalendarAndImageDisplay.Model;
using static CalendarAndImageDisplay.Model.GoogleApiManager;
using Microsoft.OpenApi.Validations;

namespace ImageAPIServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;
        private readonly string _picturesFolderPath = "./pictures";

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }

        [HttpGet("calendar")]
        [EnableCors("AllowAllOrigins")]
        public async Task<IActionResult> GetCalendar()
        {
            try
            {
                IGetCalendarServiceResponse response = await GetCalendarService();

                if (response is MyCalendarService service)
                {
                    return Ok(new { type = "calendar", content = service.GetDays(2) });
                } else if (response is DeviceAuthorization device)
                {
                    return Ok(new { type = "authentication", content = new { url = device.VerificationUrl, user_code = device.UserCode } });
                }
                return StatusCode(500, "Non of the above?");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the calendar.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("random_image")]
        [EnableCors("AllowAllOrigins")]
        public async Task<IActionResult> GetImage()
        {
            try
            {
                if (!Directory.Exists(_picturesFolderPath))
                {
                    _logger.LogError("Image directory does not exist: {Path}", _picturesFolderPath);
                    return NotFound("Image directory not found.");
                }

                // Get all image files in all subdirectories
                string[] allFiles = Directory.GetFiles(_picturesFolderPath, "*.*", SearchOption.AllDirectories)
                                        .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                       file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                       file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                       file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                                        .ToArray();

                if (allFiles.Length == 0)
                {
                    _logger.LogWarning("No valid image files found in any directory.");
                    return NotFound("No valid images found.");
                }

                Random random = new Random();
                string randomImagePath = Path.GetFullPath(allFiles[random.Next(allFiles.Length)]);

                // Read the image bytes and return as a file result


                //Console.WriteLine(randomImagePath);
                byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(randomImagePath);
                string contentType = GetContentType(randomImagePath);

                return Ok(new
                {
                    FileName = randomImagePath[8..],
                    ContentType = contentType,
                    ImageData = Convert.ToBase64String(imageBytes) // Convert to Base64 for safe JSON transport
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the random image.");
                return StatusCode(500, "Internal server error.");
            }
        }
        // Helper method to determine content type based on file extension
        private static string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}
