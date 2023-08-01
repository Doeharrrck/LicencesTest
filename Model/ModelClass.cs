using System.ComponentModel;
using System.Text;

namespace Model
{
    public class ModelClass : INotifyPropertyChanged
    {
        private string name = "Test!";

        public event PropertyChangedEventHandler PropertyChanged;

        public string ModelText => this.GetHuhu();

        private string GetHuhu()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Huch");
            sb.Replace("ch", "hu");
            sb.Append("!");

            return sb.ToString();
        }

        public string Name
        {
            get => this.name;
            set
            {
                this.name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            }
        }

        
    }
}