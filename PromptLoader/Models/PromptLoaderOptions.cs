using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PromptLoader.Models
{
    public class PromptLoaderOptions
    {
        public string PromptsFolder { get; set; }
        public string PromptSetFolder { get; set; }
        public string PromptListType { get; set; }
        public string[] PromptList { get; set; }
        public bool ConstrainPromptList { get; set; }
        public string[] SupportedPromptExtensions { get; set; }
        public string PromptSeparator { get; set; }
        public bool CascadeOverride { get; set; } = true;
        // Add more as needed
    }
}
