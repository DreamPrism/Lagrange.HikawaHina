namespace Lagrange.HikawaHina.Database
{
    internal record ModelMessage
    {
        public int Id { get; set; }
        public int MessageID { get; set; }
        public DateTime Time { get; set; }
        public long Sender { get; set; }
        public long Group { get; set; }
        public string SerializedText { get; set; }
        public int TempIndex { get; set; }
    }
}
