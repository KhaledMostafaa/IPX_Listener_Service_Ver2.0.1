namespace IpxListenerService.Models
{
    public class EventPayload
    {
        public string CameraName { get; set; }
        public string Channel { get; set; }
        public int? Gpu { get; set; }
        public string AreaName { get; set; }
        public string AnalyticType { get; set; }
        public string ObjectType { get; set; }
        public string ObjectName { get; set; }
        public int? AccumulatedCount { get; set; }
        public string Alarmed { get; set; }
        public int? Quantity { get; set; }
        public string SnapshotUrl { get; set; }
        public string SnapshotUrl2 { get; set; }
    }
}
