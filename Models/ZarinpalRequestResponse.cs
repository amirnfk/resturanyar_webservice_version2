namespace resturanyar.Models
{
    public class ZarinpalRequestResponse
    {
        public ZarinpalData Data { get; set; }
        public ZarinpalError Errors { get; set; }
    }

    public class ZarinpalData
    {
        public string Authority { get; set; }
        public int Code { get; set; }
        public string FeeType { get; set; }
        public decimal Fee { get; set; }
    }

    public class ZarinpalError
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }

}
