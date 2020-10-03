using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    public class ExternalDescriptionSpecification {
        // The name of the file to read, or a pattern identifying the file
        public string FileNamePattern { get; set; } = "info.txt";
        // Whether the result should be shown as plaintext, or a link
        public ExternalDescriptionType DescriptionType { get; set; }
        // Whether the result should come from the name of the file, or the contents of the file
        public ExternalFileType FileType { get; set; }
        // Whether to perform a regex replace on the result
        public ExternalDescriptionFilterType FilterType { get; set; }
        // If so, the regex replacement string
        public string? FilterContent { get; set; }

        public async Task<ExternalDescription?> FetchFromFolderAsync(StorageFolder folder) {
            foreach (var file in await folder.GetFilesInNaturalOrderAsync()) {
                if (Regex.IsMatch(file.Name, this.FileNamePattern)) {
                    var content = this.FileType switch {
                        ExternalFileType.FileName => file.Name,
                        ExternalFileType.Content => await ReadFileToEndAsync(file),
                        _ => throw new ProgrammerError("Unhandled switch case")
                    };

                    var filteredContent = this.FilterType switch {
                        ExternalDescriptionFilterType.RegexReplace
                            => Regex.Replace(content, this.FileNamePattern, this.FilterContent),
                        _ => content
                    };

                    return new ExternalDescription {
                        Content = filteredContent,
                        DescriptionType = this.DescriptionType
                    };
                }
            }

            return null;
        }

        private static async Task<string> ReadFileToEndAsync(StorageFile file) {
            using var stream = await file.OpenReadAsync();
            return await stream.ReadTextAsync(Encoding.UTF8);
        }

        public static List<EnumInfo<ExternalDescriptionType>> DescriptionTypeTypeNames = new List<EnumInfo<ExternalDescriptionType>> {
            EnumInfo.New("Link", ExternalDescriptionType.Link),
            EnumInfo.New("Text", ExternalDescriptionType.Text),
        };
    }

    public struct EnumInfo<E> where E: Enum {
        public string Name { get; set; }
        public E DescriptionType { get; set; }
    }

    public static class EnumInfo {
        public static EnumInfo<A> New<A>(string name, A value) where A : Enum {
            return new EnumInfo<A> {
                Name = name,
                DescriptionType = value
            };
        }
    }

    public class ExternalDescription {
        public string Content { get; set; } = "";
        public ExternalDescriptionType DescriptionType { get; set; }
    }

    public class ExternalDescriptionFilter {
        public ExternalDescriptionFilterType FilterType { get; set; }
        public string Content { get; set; } = "";
    }

    public enum ExternalDescriptionFilterType {
        None, RegexReplace
    }

    public enum ExternalDescriptionType {
        Text, Link
    }

    public enum ExternalFileType {
        FileName, Content
    }
}
