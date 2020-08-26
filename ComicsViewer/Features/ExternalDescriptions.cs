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
        public string FileNamePattern { get; set; } = "info.txt";
        public ExternalDescriptionType DescriptionType { get; set; }
        public ExternalFileType FileType { get; set; }
        public ExternalDescriptionFilter Filter { get; set; }
            = new ExternalDescriptionFilter { FilterType = ExternalDescriptionFilterType.None };

        public async Task<ExternalDescription?> FetchFromFolderAsync(StorageFolder folder) {
            foreach (var file in await folder.GetFilesAsync()) {
                if (Regex.IsMatch(file.Name, this.FileNamePattern)) {
                    var content = this.FileType switch {
                        ExternalFileType.FileName => file.Name,
                        ExternalFileType.Content => await ReadFileToEndAsync(file),
                        _ => throw new ProgrammerError("Unhandled switch case")
                    };

                    var filteredContent = this.Filter.FilterType switch {
                        ExternalDescriptionFilterType.RegexReplace
                            => Regex.Replace(content, this.FileNamePattern, this.Filter.Content),
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
