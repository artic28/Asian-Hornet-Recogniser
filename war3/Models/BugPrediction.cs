using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace war3.Models
{
    public class BugPrediction
    {
        [ColumnName("model_outputs0")]
        public float[] BugType { get; set; }
    }
}
