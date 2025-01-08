
using Microsoft.AspNetCore.Mvc;

using System.Data;
using Microsoft.Data.SqlClient;

using System.Text.Json;
using filetransferBO.Models;

namespace filetransfer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        public FilesController(IConfiguration configuration)
        {
            _configuration = configuration;

            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }
        [NonAction]
        public string GetConnectionString()
        {
            return _connectionString;
        }


        // 1. Upload File API
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] int senderId, [FromForm] int receiverId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var sentFilesPath = Path.Combine("chitchat", "sent_files");
            Directory.CreateDirectory(sentFilesPath);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var sentFilePath = Path.Combine(sentFilesPath, fileName);

            using (var stream = new FileStream(sentFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            SaveFileMetadata(sentFilePath, senderId, receiverId); // Call to save metadata

            return Ok(new { FileName = fileName });
        }
        [NonAction]
        private void SaveFileMetadata(string sentFilePath, int senderId, int receiverId)
        {
            var jsonInput = new
            {
                SenderFilePath = sentFilePath,
                ReceiverFilePath = $"chitchat/received_files/{System.IO.Path.GetFileName(sentFilePath)}",
                SenderId = senderId,
                ReceiverId = receiverId,
                Downloaded = false
            };

            string jsonString = JsonSerializer.Serialize(jsonInput);

            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                using (SqlCommand command = new SqlCommand("SaveFileMetadata", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@JsonInput", jsonString));

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        // 2. Download File API
        [HttpGet("download/{fileId}")]
        public IActionResult DownloadFile(int fileId)
        {
            var fileMetadata = GetFileMetadata(fileId); // Fetch metadata from DB
            if (fileMetadata == null)
                return NotFound("File not found.");

            if (!System.IO.File.Exists(fileMetadata.SenderFilePath))
                return BadRequest("The sender has deleted the original file.");

            var tempFilesPath = Path.Combine("chitchat", "temp_files");
            Directory.CreateDirectory(tempFilesPath);

            var tempFilePath = Path.Combine(tempFilesPath, Path.GetFileName(fileMetadata.SenderFilePath));
            System.IO.File.Copy(fileMetadata.SenderFilePath, tempFilePath, true);

            MarkAsDownloaded(fileId); // Implement this method to mark the file as downloaded

            var fileBytes = System.IO.File.ReadAllBytes(tempFilePath);
            return File(fileBytes, "application/octet-stream", Path.GetFileName(tempFilePath));
        }
        [NonAction]
        private FileMetadata GetFileMetadata(int fileId)
        {
            FileMetadata fileMetadata = null;

            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                using (SqlCommand command = new SqlCommand("SELECT Id, SenderFilePath, ReceiverFilePath, SenderId, ReceiverId, Downloaded, UploadDate FROM Files WHERE Id = @FileId", connection))
                {
                    command.Parameters.Add(new SqlParameter("@FileId", fileId));

                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            fileMetadata = new FileMetadata
                            {
                                Id = reader.GetInt32(0),
                                SenderFilePath = reader.GetString(1),
                                ReceiverFilePath = reader.GetString(2),
                                SenderId = reader.GetInt32(3),
                                ReceiverId = reader.GetInt32(4),
                                Downloaded = reader.GetBoolean(5),
                                UploadDate = reader.GetDateTime(6)
                            };
                        }
                    }
                }
            }

            return fileMetadata;
        }
        [NonAction]
        private void MarkAsDownloaded(int fileId)
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                using (SqlCommand command = new SqlCommand("UPDATE Files SET Downloaded = 1 WHERE Id = @FileId", connection))
                {
                    command.Parameters.Add(new SqlParameter("@FileId", fileId));

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }


        // 3. Get Received Files API
        [HttpGet("received/{userId}")]
        public IActionResult GetReceivedFiles(int userId)
        {
            var receivedFiles = new List<FileMetadata>();

            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                using (SqlCommand command = new SqlCommand("GetReceivedFiles", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@UserId", userId));

                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var file = new  FileMetadata
                            {
                                Id = reader.GetInt32(0),
                                SenderFilePath = reader.GetString(1),
                                ReceiverFilePath = reader.GetString(2),
                                SenderId = reader.GetInt32(3),
                                ReceiverId = reader.GetInt32(4),
                                Downloaded = reader.GetBoolean(5),
                                UploadDate = reader.GetDateTime(6)
                            };
                            receivedFiles.Add(file);
                        }
                    }
                }
            }

            return Ok(receivedFiles);
        }
    }

    
}
