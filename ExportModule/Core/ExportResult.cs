namespace ExportModule.Core
{
    public class ExportResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public TimeSpan Duration { get; set; }
        public string OutputPath { get; set; }


        public static ExportResult Success(string outputPath, TimeSpan duration)
        {
            return new()
            {
                IsSuccess = true,
                Duration = duration,
                OutputPath = outputPath,
            };
        }


        public static ExportResult Failure(string message)
        {
            return new() 
            { 
                IsSuccess = false, 
                Message = message 
            };
        }



    }
}