using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApplication1
{
    public class AirBNBRecord
    {
        public string Headline { get; set; }
        public string Link { get; set; }
        public int ReviewCount { get; set; }
        public int Price { get; set; }

        public string Total { get; set; }

        public string Policy { get; set; }
    }
}
