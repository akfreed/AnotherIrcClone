// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================


namespace ChatCommon
{
    /// <summary>
    /// This simple class is used to store an operation success indicator and message.
    /// </summary>
    public class Response
    {
        public bool   Success { get; set; }
        public string Message { get; set; }

        public Response(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}
