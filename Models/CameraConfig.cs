using System;

namespace IpxListenerService.Models
{
    public class CameraConfig
    {
        public string Name { get; set; }
        public int Gpu { get; set; }
        public string Channel { get; set; }
        public bool Active { get; set; }
        public string Description { get; set; }
        public string XmlSourceFile { get; set; }
        public DateTime XmlLastModified { get; set; }
    }
}
