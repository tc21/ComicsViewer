﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComicsViewer.Common;

namespace ComicsViewer.Support {

    public static class NavigationTags {
        public static NavigationTag FromTagName(string tagName) {
            return tagName switch {
                "comics" => NavigationTag.Comics,
                "authors" => NavigationTag.Author,
                "categories" => NavigationTag.Category,
                "tags" => NavigationTag.Tags,
                "default" => NavigationTag.Detail,
                _ => throw new NotImplementedException(),
            };
        }
    }

    public enum NavigationTag {
        Comics, Author, Category, Tags, Detail
    }

    public static class NavigationTag_ToTagName {
        public static string ToTagName(this NavigationTag tag) {
            return tag switch {
                NavigationTag.Comics => "comics",
                NavigationTag.Author => "authors",
                NavigationTag.Category => "categories",
                NavigationTag.Tags => "tags",
                NavigationTag.Detail => "default",
                _ => throw new ProgrammerError("unhandled switch case")
            };
        }

        public static string Describe(this NavigationTag tag, bool capitalized = false) {
            return tag switch {
                NavigationTag.Comics => capitalized ? "Item" : "item",
                NavigationTag.Author => capitalized ? "Author": "author",
                NavigationTag.Category => capitalized ? "Category" : "category",
                NavigationTag.Tags => capitalized ? "Tag" : "tag",
                NavigationTag.Detail => capitalized ? "Item" : "item",
                _ => throw new ProgrammerError("unhandled switch case")
            };
        }

        public static bool IsWorkItemNavigationTag(this NavigationTag tag) {
            return (tag == NavigationTag.Comics || tag == NavigationTag.Detail);
        }
    }
}
