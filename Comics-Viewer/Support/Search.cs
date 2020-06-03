﻿using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer {
    static class Search {
        /* Note: This can be optimized a lot by relying on the cached data in ComicStore. We'll see if it comes to it */
        /* Note 2: We are currently ANDing every search term. It's probably better to create a filter UI than to 
         * implement AND/OR keywords into the search box */
        static Dictionary<string, Func<Comic, string>> searchFields = new Dictionary<string, Func<Comic, string>> {
            { "title", (comic) => comic.Title },
            { "author", (comic) => comic.Author },
            { "category", (comic) => comic.Category },
            { "tags", (comic) => string.Join("|", comic.Tags) } // We should just return a list, but we'll assume no one will actually use "|" for now...
        };

        static string DefaultSearchField(Comic comic) {
            return comic.UniqueIdentifier;
        }

        static internal Func<Comic, bool> Compile(string searchTerm) {
            var requiredSearches = new List<Func<Comic, bool>>();

            List<Tuple<string, string>> tokens;

            try {
                tokens = SplitTokens(searchTerm);
            } catch (ArgumentException) {
                // return null to indicate error
                return null;
            }

            foreach (var token in tokens) {
                if (token.Item1 == "") {
                    requiredSearches.Add(comic => DefaultSearchField(comic).Contains(token.Item2, StringComparison.InvariantCultureIgnoreCase));
                    continue;
                }

                if (!searchFields.ContainsKey(token.Item1)) {
                    // Encountering non-existent token
                    return null;
                }
                

                requiredSearches.Add(comic => searchFields[token.Item1](comic).Contains(token.Item2, StringComparison.InvariantCultureIgnoreCase));
            }

            return comic => requiredSearches.All(search => search(comic));
        }

        static internal List<string> GetSearchSuggestions(string incompleteSearchTerm) {
            return new List<string>();
        }

        static List<Tuple<string, string>> SplitTokens(string searchTerm) {
            var result = new List<Tuple<string, string>>();

            /* Very primitive parser for a very simple task */

            var lastToken = "";
            var parserCache = "";
            var parserMode = "initial";

            foreach (var character in searchTerm) {
                if (parserMode == "string") {
                    if (character == '"') {
                        parserMode = "string-end";
                    } else {
                        parserCache += character;
                    }
                    continue;
                }

                if (parserMode == "string-end" && !": ".Contains(character)) {
                    throw new ArgumentException("Cannot mix quoted and non-quoted strings");
                }

                if (character == '"') {
                    if (parserCache != "") {
                        throw new ArgumentException("Cannot mix quoted and non-quoted strings");
                    }

                    parserMode = "string";
                    continue;
                }

                if (character == ':') {
                    if (parserMode == "argument") {
                        throw new ArgumentException("Argument indicator ':' cannot appear twice in an argument");
                    }
                    lastToken = parserCache;
                    parserCache = "";
                    parserMode = "argument";
                    continue;
                }

                if (character == ' ') {
                    if (parserCache != "") {
                        result.Add(new Tuple<string, string>(lastToken, parserCache));
                        lastToken = "";
                        parserCache = "";
                        parserMode = "initial";
                    }
                    continue;
                }

                parserCache += character;
            }

            if (parserCache != "") {
                result.Add(new Tuple<string, string>(lastToken, parserCache));
            }

            return result;
        }
    }
}
