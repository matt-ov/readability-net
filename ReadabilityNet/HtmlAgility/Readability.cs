using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HtmlAgilityPack
{
    public class Readability
    {
        public int ContentScore { get; set; }

        public Readability(int contentScore)
        {
            this.ContentScore = contentScore;
        }
    }
}
