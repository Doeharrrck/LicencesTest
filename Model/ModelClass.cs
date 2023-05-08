using System.Text;

namespace Model
{
    public class ModelClass
    {
        public string ModelText => this.GetHuhu();

        private string GetHuhu()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Huch");
            sb.Replace("ch", "hu");
            sb.Append("!");

            return sb.ToString();
        }
    }
}