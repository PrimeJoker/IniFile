﻿#region --- License & Copyright Notice ---
/*
IniFile Library for .NET
Copyright (c) 2018 Jeevan James
All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IniFile.Items;

namespace IniFile
{
    /// <summary>
    ///     <para>In-memory object representation of an INI file.</para>
    ///     <para>This class is a read-only collection of <see cref="Section"/> objects.</para>
    /// </summary>
    [DebuggerDisplay("INI file - {Count} sections")]
    public sealed partial class Ini
    {
        /// <summary>
        ///     Initializes a new empty instance of the <see cref="Ini"/> class with the default
        ///     settings.
        /// </summary>
        public Ini() : this(null)
        {
        }

        /// <summary>
        ///     Initializes a new empty instance of the <see cref="Ini"/> class with the specified
        ///     settings.
        /// </summary>
        /// <param name="settings">The Ini file settings.</param>
        public Ini(IniLoadSettings settings) : base(GetEqualityComparer(settings))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Ini"/> class and loads the data from
        ///     the specified file.
        /// </summary>
        /// <param name="iniFile">The .ini file to load from.</param>
        /// <param name="settings">Optional Ini file settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if the <c>iniFile</c> is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
        public Ini(FileInfo iniFile, IniLoadSettings settings = null) : base(GetEqualityComparer(settings))
        {
            if (iniFile == null)
                throw new ArgumentNullException(nameof(iniFile));
            if (!iniFile.Exists)
                throw new FileNotFoundException($"INI file '{iniFile.FullName}' does not exist", iniFile.FullName);

            using (var reader = new StreamReader(iniFile.FullName, settings.Encoding ?? Encoding.UTF8, settings.DetectEncoding))
                ParseIniFile(reader);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Ini"/> class and loads the data from
        ///     the specified file.
        /// </summary>
        /// <param name="iniFilePath">The path to the .ini file.</param>
        /// <param name="settings">Optional Ini file settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if the <c>iniFilePath</c> is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
        public Ini(string iniFilePath, IniLoadSettings settings = null) : base(GetEqualityComparer(settings))
        {
            if (iniFilePath == null)
                throw new ArgumentNullException(nameof(iniFilePath));
            if (!File.Exists(iniFilePath))
                throw new FileNotFoundException($"INI file '{iniFilePath}' does not exist", iniFilePath);

            using (var reader = new StreamReader(iniFilePath, settings.Encoding ?? Encoding.UTF8, settings.DetectEncoding))
                ParseIniFile(reader);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Ini"/> class and loads the data from
        ///     the specified stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="settings">Optional Ini file settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if the specified stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the stream cannot be read.</exception>
        public Ini(Stream stream, IniLoadSettings settings = null) : base(GetEqualityComparer(settings))
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanRead)
                throw new ArgumentException("Cannot read from specified stream", nameof(stream));

            using (var reader = new StreamReader(stream, settings.Encoding ?? Encoding.UTF8, settings.DetectEncoding))
                ParseIniFile(reader);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Ini"/> class and loads the data from
        ///     the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> to load from.</param>
        /// <param name="settings">Optional Ini file settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if the specified text reader is null.</exception>
        public Ini(TextReader reader, IniLoadSettings settings = null) : base(GetEqualityComparer(settings))
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            ParseIniFile(reader);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Ini"/> class and loads the data from
        ///     the specified string.
        /// </summary>
        /// <param name="content">The string representing the Ini content.</param>
        /// <param name="settings">Optional Ini file settings.</param>
        /// <returns>An instance of the <see cref="Ini"/> class.</returns>
        public static Ini Load(string content, IniLoadSettings settings = null)
        {
            using (var reader = new StringReader(content))
                return new Ini(reader, settings);
        }

        /// <summary>
        ///     Transforms the INI content into an in-memory object model.
        /// </summary>
        /// <param name="reader">The INI content.</param>
        private void ParseIniFile(TextReader reader)
        {
            // Read all lines from the INI content and transform to one of the registered objects -
            // section, property, comment or blank line.
            var allItems = new List<IniItem>();
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                allItems.Add(IniItemFactory.CreateItem(line));

            // Go through each line object and construct a hierarchical object model, with properties
            // under sections and comments/blank lines under respective sections and properties.
            Section currentSection = null;
            var minorItems = new List<MinorIniItem>();
            foreach (IniItem item in allItems)
            {
                // If the current line is a property, but we're not in a section, then this is invalid.
                if (item is Property prop && currentSection == null)
                    throw new FormatException($"Property '{prop.Name}' is not in a section.");

                if (item is MinorIniItem minorItem)
                    minorItems.Add(minorItem);
                else if (item is Section section)
                {
                    currentSection = section;
                    AddRangeAndClear(currentSection.Items, minorItems);
                    Add(currentSection);
                }
                else if (item is Property property)
                {
                    AddRangeAndClear(property.Items, minorItems);
                    currentSection.Add(property);
                }
            }

            if (minorItems.Count > 0)
                AddRangeAndClear(TrailingItems, minorItems);
        }

        private static void AddRangeAndClear(IList<MinorIniItem> source, IList<MinorIniItem> minorItems)
        {
            foreach (MinorIniItem item in minorItems)
                source.Add(item);
            minorItems.Clear();
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to a file at the specified file path.
        /// </summary>
        /// <param name="filePath">The path of the file to save to.</param>
        public void SaveTo(string filePath)
        {
            using (StreamWriter writer = File.CreateText(filePath))
                SaveTo(writer);
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to the specified file.
        /// </summary>
        /// <param name="file">The <see cref="FileInfo"/> object that represents the file to save to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the specified file is null.</exception>
        public void SaveTo(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            using (StreamWriter writer = File.CreateText(file.FullName))
                SaveTo(writer);
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to save to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the specified stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the stream cannot be written to.</exception>
        public void SaveTo(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Cannot write to the specified stream.", nameof(stream));
            using (var writer = new StreamWriter(stream))
                SaveTo(writer);
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to the specified stream asynchronously.
        /// </summary>
        /// <param name="stream">The stream to save to.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the stream cannot be written to.</exception>
        public async Task SaveToAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Cannot write to the specified stream.", nameof(stream));
            using (var writer = new StreamWriter(stream))
                await SaveToAsync(writer);
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to the specified text writer.
        /// </summary>
        /// <param name="writer">The text writer to save to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the specified text writer is null.</exception>
        public void SaveTo(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            InternalSave(writer);
            writer.Flush();
        }

        /// <summary>
        ///     Saves the <see cref="Ini"/> instance to the specified text writer asynchronously.
        /// </summary>
        /// <param name="writer">The text writer to save to.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified text writer is null.</exception>
        public async Task SaveToAsync(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            await InternalSaveAsync(writer);
            writer.Flush();
        }

        /// <summary>
        ///     Constructs a string representing the <see cref="Ini"/> instance data.
        /// </summary>
        /// <returns>A string representing the <see cref="Ini"/> instance data.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
                InternalSave(writer);
            return sb.ToString();
        }

        /// <summary>
        ///     Common method called to save the <see cref="Ini"/> instance data to various destinations
        ///     such as streams, strings, files and text writers.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        private void InternalSave(TextWriter writer)
        {
            foreach (Section section in this)
            {
                foreach (MinorIniItem minorItem in section.Items)
                    writer.WriteLine(minorItem.ToString());
                writer.WriteLine(section.ToString());
                foreach (Property property in section)
                {
                    foreach (MinorIniItem minorItem in property.Items)
                        writer.WriteLine(minorItem.ToString());
                    writer.WriteLine(property.ToString());
                }
            }

            foreach (MinorIniItem trailingItem in TrailingItems)
                writer.WriteLine(trailingItem.ToString());
        }

        /// <summary>
        ///     Common method called to asynchronously  save the <see cref="Ini"/> instance data to
        ///     various destinations such as streams, strings, files and text writers.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        private async Task InternalSaveAsync(TextWriter writer)
        {
            foreach (Section section in this)
            {
                foreach (MinorIniItem minorItem in section.Items)
                    await writer.WriteLineAsync(minorItem.ToString());
                writer.WriteLine(section.ToString());
                foreach (Property property in section)
                {
                    foreach (MinorIniItem minorItem in property.Items)
                        await writer.WriteLineAsync(minorItem.ToString());
                    writer.WriteLine(property.ToString());
                }
            }

            foreach (MinorIniItem trailingItem in TrailingItems)
                await writer.WriteLineAsync(trailingItem.ToString());
        }

        /// <summary>
        ///     Any trailing comments and blank lines at the end of the INI.
        /// </summary>
        public IList<MinorIniItem> TrailingItems { get; } = new List<MinorIniItem>();

        /// <summary>
        ///     Formats the INI content by resetting all padding and applying any formatting rules
        ///     as per the <c>options</c> parameter.
        /// </summary>
        /// <param name="options">
        ///     Optional rules for formatting the INI content. Uses default rules
        ///     (<see cref="IniFormatOptions.Default"/>) if not specified.
        /// </param>
        public void Format(IniFormatOptions options = null)
        {
            options = options ?? IniFormatOptions.Default;

            for (int s = 0; s < this.Count; s++)
            {
                Section section = this[s];

                // Reset padding for each minor item in the section
                foreach (MinorIniItem minorItem in section.Items)
                {
                    if (minorItem is BlankLine blankLine)
                        blankLine.Padding.Reset();
                    else if (minorItem is Comment comment)
                        comment.Padding.Reset();
                }

                // Insert blank line between sections, if specified by options
                if (options.EnsureBlankLineBetweenSections)
                {
                    if (s > 0 && (!section.Items.Any() || !(section.Items[0] is BlankLine)))
                        section.Items.Insert(0, new BlankLine());
                }

                // Reset padding for the section itself
                section.Padding.Reset();

                for (int p = 0; p < section.Count; p++)
                {
                    Property property = section[p];

                    // Reset padding for each minor item in the property
                    foreach (MinorIniItem minorItem in property.Items)
                    {
                        if (minorItem is BlankLine blankLine)
                            blankLine.Padding.Reset();
                        else if (minorItem is Comment comment)
                            comment.Padding.Reset();
                    }

                    // Insert blank line between properties, if specified
                    if (options.EnsureBlankLineBetweenProperties)
                    {
                        if (p > 0 && (!property.Items.Any() || !(property.Items[0] is BlankLine)))
                            property.Items.Insert(0, new BlankLine());
                    }

                    // Reset padding for the property itself
                    property.Padding.Reset();
                }
            }

            // Remove any trailing blank lines
            for (int i = TrailingItems.Count - 1; i >= 0; i--)
            {
                // Non blank lines are fine, so when we encounter the first non blank line, exit
                // this loop.
                if (!(TrailingItems[i] is BlankLine))
                    break;
                if (TrailingItems[i] is BlankLine)
                    TrailingItems.RemoveAt(i);
            }

            // Format any remaining trailing items
            foreach (MinorIniItem trailingItem in TrailingItems)
            {
                if (trailingItem is BlankLine blankLine)
                    blankLine.Padding.Reset();
                else if (trailingItem is Comment comment)
                    comment.Padding.Reset();
            }
        }
    }

    public sealed partial class Ini : KeyedCollection<string, Section>
    {
        protected override string GetKeyForItem(Section item) =>
            item.Name;

        private static IEqualityComparer<string> GetEqualityComparer(IniLoadSettings settings)
        {
            settings = settings ?? IniLoadSettings.Default;
            return settings.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        }
    }
}