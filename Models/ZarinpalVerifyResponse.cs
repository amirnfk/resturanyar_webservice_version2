using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace resturanyar.Models
{
    public class ZarinpalVerifyResponse
    {
        [JsonProperty("data")]
        public ZarinpalVerifyData Data { get; set; }
    }

    public class ZarinpalVerifyData
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("ref_id")]
        public long? RefId { get; set; }

        [JsonProperty("card_pan")]
        public string CardPan { get; set; }
    }
}
