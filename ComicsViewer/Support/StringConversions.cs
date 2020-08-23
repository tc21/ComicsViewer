using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Support {
    /* remarks: I started writing this class in the hopes that it will be helpful later on,
     * since it can be generic, and used anywhere functionality like this is needed. 
     * However, currently we are essentially writing 50 lines of code so that we can call 
     * external code when we want to do
     *     string.join(", ", list)
     * and
     *     str.split(',').Select(s => s.Trim())
     * 
     * I question the usefulness of this class. Today is 8/23/2020. If we go 50 commits or 1 year
     * without adding at least two more subclasses of StringConvertible, we should delete this class.
     */
    public static class StringConversions {
        public static readonly DelimitedList CommaDelimitedList = new DelimitedList(',');

        public class DelimitedList : StringConvertible<IEnumerable<string>> {
            private readonly char separator;
            private readonly bool canTrimWhitespace;

            public DelimitedList(char separator, bool canTrimWhitespace = true) {
                this.separator = separator;
                this.canTrimWhitespace = canTrimWhitespace;
            }

            public override IEnumerable<string> Convert(string str) {
                var split = str.Split(this.separator);
                if (this.canTrimWhitespace) {
                    return split.Select(s => s.Trim());
                } else {
                    return split;
                }
            }

            public override string ConvertToString(IEnumerable<string> value) {
                if (this.canTrimWhitespace) {
                    return string.Join($"{this.separator} ", value);
                } else {
                    return string.Join(this.separator, value);
                }
            }

            public override ValidateResult CanConvert(string str) => true;
        }
    }

    public abstract class StringConvertible<T> {
        public abstract string ConvertToString(T value);
        public abstract T Convert(string str);
        public abstract ValidateResult CanConvert(string str);

    }
}
