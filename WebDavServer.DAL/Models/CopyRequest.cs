﻿namespace WebDavServer.FileStorage.Models
{
    public class CopyRequest
    {
        public string SrcDrive { get; set; }
        public string SrcPath { get; set; }
        public string DstDrive { get; set; }
        public string DstPath { get; set; }
    }
}
