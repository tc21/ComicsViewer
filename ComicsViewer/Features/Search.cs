﻿using System;
using System.Collections.Generic;
using System.Linq;
using ComicsLibrary;

#nullable enable

namespace ComicsViewer.Features {
    public static class Search {
        /* Note: This can be optimized a lot by relying on the cached data in ComicStore. We'll see if it comes to it */
        /* Note 2: We are currently ANDing every search term. It's probably better to create a filter UI than to 
         * implement AND/OR keywords into the search box */
        private static readonly Dictionary<string, Func<Comic, string>> SearchFields = new() {
            { "title", comic => comic.DisplayTitle },
            { "author", comic => comic.Author },
            { "category", comic => comic.Category },
            { "tag", comic => string.Join("|", comic.Tags) }, // We should just return a list, but we'll assume no one will actually use "|" for now...
            { "loved", comic => comic.Loved.ToString() }
        };

        private static string DefaultSearchField(Comic comic) {
            return $"{comic.Author}|{comic.DisplayTitle}";
        }

        /// <summary>
        /// Returns null when compilation failed
        /// </summary>
        public static Func<Comic, bool>? Compile(string searchTerm) {
            var requiredSearches = new List<Func<Comic, bool>>();

            var (tokens, error) = SplitTokens(searchTerm);

            if (error != null) {
                return null;
            }

            foreach (var (key, value) in tokens) {
                if (key == "") {
                    requiredSearches.Add(comic => DefaultSearchField(comic).Contains(value, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                var lower = key.ToLower();

                if (!SearchFields.ContainsKey(lower)) {
                    // Encountering non-existent token
                    return null;
                }


                requiredSearches.Add(comic => SearchFields[lower](comic).Contains(value, StringComparison.OrdinalIgnoreCase));
            }

            return comic => requiredSearches.All(search => search(comic));
        }

        public static IEnumerable<string> GetSearchSuggestions(string incompleteSearchTerm, string profileName) {
            if (incompleteSearchTerm.Trim() == "") {
                return Defaults.SettingsAccessor.GetSavedSearches(profileName);
            }

            /* We have 3 types of search suggestions. Only one type runs at a time, in this priority:
             * 1. Correcting parse errors
             * 2. Correcting tag names
             * 3. Adding tags to raw strings
             *  3.1 Quoting a raw string with the previous group
             * 
             * See implementations below.
             */

            var (tokens, error) = SplitTokens(incompleteSearchTerm, tryCorrectingErrors: true);

            if (error != null) {
                // parse error
                return new[] { Decompile(tokens) };
            }

            foreach (var token in tokens) {
                if (token.key != "" && !SearchFields.ContainsKey(token.key.ToLower())) {
                    // Invalid tag name
                    return from key in SearchFields.Keys
                           orderby Similarity(key, token.key)
                           select Decompile(Replacing(tokens, token, (key, token.value)));
                }
            }

            foreach (var token in tokens) {
                if (token.key != "") {
                    continue;
                }

                // key == "" indicates raw string 
                var tagNameSuggestions =
                    (from key in SearchFields.Keys
                     orderby key
                     select Decompile(Replacing(tokens, token, (key, token.value)))).ToList();

                var index = tokens.IndexOf(token);

                if (index > 0) {
                    var (key, value) = tokens[index - 1];
                    tokens[index - 1] = (key, $"{value} {token.value}");
                    tokens.RemoveAt(index);
                } else if (tokens.Count > (index + 1) && tokens[index + 1].key == "") {
                    tokens[index] = (token.key, $"{token.value} {tokens[index + 1].value}");
                    tokens.RemoveAt(index + 1);
                }

                tagNameSuggestions.Insert(0, Decompile(tokens));

                return tagNameSuggestions;
            }

            return new string[0];

            // A bunch of helper methods
            static int Similarity(string key, string test) {
                if (key.StartsWith(test, StringComparison.OrdinalIgnoreCase)) {
                    return key.Length - test.Length;
                }

                return 10 * LevenshteinDistance(key, test);
            }

            static string Decompile(IEnumerable<(string key, string value)> tokens) {
                return string.Join(' ', tokens.Select(DecompileToken));
            }

            static string DecompileToken((string key, string value) token) {
                if (token.key == "") {
                    return Quote(token.value);
                }
                return $"{Quote(token.key)}:{Quote(token.value)}";
            }

            static string Quote(string str) {
                if (": ".Any(str.Contains)) {
                    return $"\"{str}\"";
                }

                return str;
            }

            static IEnumerable<T> Replacing<T>(IEnumerable<T> enumerable, T originalValue, T newValue) where T : IEquatable<T> {
                foreach (var item in enumerable) {
                    if (item.Equals(originalValue)) {
                        yield return newValue;
                    } else {
                        yield return item;
                    }
                }
            }

            static int LevenshteinDistance(string a, string b) {
                if (a == "") {
                    return b.Length;
                }

                if (b == "") {
                    return a.Length;
                }

                if (a.Substring(0, 1).Equals(b.Substring(0, 1), StringComparison.OrdinalIgnoreCase)) {
                    return LevenshteinDistance(a.Substring(1), b.Substring(1));
                }

                return 1 + Math.Min(
                    Math.Min(
                        LevenshteinDistance(a, b.Substring(1)),
                        LevenshteinDistance(a.Substring(1), b)
                    ),
                    LevenshteinDistance(a.Substring(1), b.Substring(1))
                );
            }
        }

        private static (List<(string key, string value)> tokens, string? error) SplitTokens(string searchTerm, bool tryCorrectingErrors = false) {
            var result = new List<(string key, string value)>();

            if (searchTerm.Contains('|')) {
                return (result, error: "Invalid character '|'");
            }

            /* Very primitive parser for a very simple task */

            var lastToken = "";
            var parserCache = "";
            var parserMode = "initial";
            var parserIndex = 0;

            for (; parserIndex < searchTerm.Length; parserIndex++) {
                var character = searchTerm[parserIndex];

                switch (parserMode) {
                    case "string": {
                        if (character == '"') {
                            parserMode = "string-end";
                        } else {
                            parserCache += character;
                        }
                        continue;
                    }
                    case "string-end" when !": ".Contains(character):
                        return ReturnValueOnError("Cannot mix quoted and non-quoted strings");
                    default:
                        break;
                }

                switch (character) {
                    case '"' when parserCache != "":
                        return ReturnValueOnError("Cannot mix quoted and non-quoted strings");
                    case '"':
                        parserMode = "string";
                        continue;
                    case ':' when parserMode == "argument":
                        return ReturnValueOnError("Argument indicator ':' cannot appear twice in an argument");
                    case ':':
                        lastToken = parserCache;
                        parserCache = "";
                        parserMode = "argument";
                        continue;
                    case ' ': {
                        if (parserCache != "") {
                            PushCompletedToken();
                        }
                        continue;
                    }
                    default:
                        parserCache += character;
                        break;
                }
            }

            if (parserCache != "") {
                PushCompletedToken();
            }

            return (result, error: null);

            // Helper functions
            void PushCompletedToken() {
                result.Add((key: lastToken, value: parserCache));
                lastToken = "";
                parserCache = "";
                parserMode = "initial";
            }

            (List<(string key, string value)> tokens, string? error) ReturnValueOnError(string error) {
                if (tryCorrectingErrors) {
                    var remainingSearchTerm = (parserCache + searchTerm.Substring(parserIndex)).Replace("\"", "");
                    result.Add((key: lastToken, value: remainingSearchTerm));
                }

                return (result, error);
            }
        }
    }
}
