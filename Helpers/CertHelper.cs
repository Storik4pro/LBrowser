using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LinesBrowser.Helpers
{
    public class CertHelper
    {
        public static Dictionary<string, string> ParseDistinguishedName(string distinguishedName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(distinguishedName))
                return result;

            string[] parts = distinguishedName.Split(',');
            foreach (var rawPart in parts)
            {
                var part = rawPart.Trim();
                int idx = part.IndexOf('=');
                if (idx > 0 && idx < part.Length - 1)
                {
                    string key = part.Substring(0, idx).Trim();
                    string value = part.Substring(idx + 1).Trim();
                    if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                        value = value.Substring(1, value.Length - 2);

                    if (!result.ContainsKey(key))
                        result[key] = value;
                }
            }

            return result;
        }
    }
}
