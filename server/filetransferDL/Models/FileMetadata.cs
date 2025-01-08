namespace filetransferBO.Models
{
    public class FileMetadata
    {
        public int Id { get; set; }
        public string SenderFilePath { get; set; }
        public string ReceiverFilePath { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public bool Downloaded { get; set; }
        public DateTime UploadDate { get; set; }
    }

}
