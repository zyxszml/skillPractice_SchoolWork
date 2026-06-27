namespace Iso11820Simulator.Models
{
    /// <summary>样品信息</summary>
    public class ProductMaster
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string Specific { get; set; }
        public double Diameter { get; set; }
        public double Height { get; set; }
        public string Flag { get; set; }
    }
}
