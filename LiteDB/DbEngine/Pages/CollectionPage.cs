﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LiteDB
{
    /// <summary>
    /// Represents the collection page AND a collection item, because CollectionPage represent a Collection (1 page = 1 collection). All collections pages are linked with Prev/Next links
    /// </summary>
    internal class CollectionPage : BasePage
    {
        /// <summary>
        /// Represent maximun bytes that all collections names can be used in header
        /// </summary>
        public const ushort MAX_COLLECTIONS_SIZE = 3000;

        public static Regex NamePattern = new Regex(@"^[\w-]{1,30}$");

        /// <summary>
        /// Page type = Collection
        /// </summary>
        public override PageType PageType { get { return PageType.Collection; } }

        /// <summary>
        /// Name of collection
        /// </summary>
        public string CollectionName { get; set; }

        /// <summary>
        /// Get a reference for the free list data page - its private list per collection - each DataPage contains only data for 1 collection (no mixing)
        /// Must to be a Field to be used as parameter reference
        /// </summary>
        public uint FreeDataPageID;

        /// <summary>
        /// Get the number of documents inside this collection
        /// </summary>
        public long DocumentCount { get; set; }

        /// <summary>
        /// Get all indexes from this collection - includes non-used indexes
        /// </summary>
        public CollectionIndex[] Indexes { get; set; }

        public CollectionPage(uint pageID)
            : base(pageID)
        {
            this.FreeDataPageID = uint.MaxValue;
            this.DocumentCount = 0;
            this.ItemCount = 1; // fixed for CollectionPage
            this.FreeBytes = 0; // no free bytes on collection-page - only one collection per page
            this.Indexes = new CollectionIndex[CollectionIndex.INDEX_PER_COLLECTION];

            for (var i = 0; i < Indexes.Length; i++)
            {
                this.Indexes[i] = new CollectionIndex() { Page = this, Slot = i };
            }
        }

        /// <summary>
        /// Update freebytes + items count
        /// </summary>
        public override void UpdateItemCount()
        {
            this.ItemCount = 1; // fixed for CollectionPage
            this.FreeBytes = 0; // no free bytes on collection-page - only one collection per page
        }

        #region Read/Write pages

        protected override void ReadContent(ByteReader reader)
        {
            this.CollectionName = reader.ReadString();
            this.FreeDataPageID = reader.ReadUInt32();
            var uintCount = reader.ReadUInt32(); // read as uint (4 bytes)

            foreach (var index in this.Indexes)
            {
                index.Field = reader.ReadString();
                index.HeadNode = reader.ReadPageAddress();
                index.TailNode = reader.ReadPageAddress();
                index.FreeIndexPageID = reader.ReadUInt32();
                index.Options.Unique = reader.ReadBoolean();
                index.Options.IgnoreCase = reader.ReadBoolean();
                index.Options.TrimWhitespace = reader.ReadBoolean();
                index.Options.EmptyStringToNull = reader.ReadBoolean();
                index.Options.RemoveAccents = reader.ReadBoolean();
            }

            // be compatible with v2_beta
            var longCount = reader.ReadInt64();
            this.DocumentCount = Math.Max(uintCount, longCount);

        }

        protected override void WriteContent(ByteWriter writer)
        {
            writer.Write(this.CollectionName);
            writer.Write(this.FreeDataPageID);

            // to be compatible with v2_beta, write here only uint (4 bytes)
            writer.Write(this.DocumentCount > uint.MaxValue ?
                uint.MaxValue : (uint)this.DocumentCount);

            foreach (var index in this.Indexes)
            {
                writer.Write(index.Field);
                writer.Write(index.HeadNode);
                writer.Write(index.TailNode);
                writer.Write(index.FreeIndexPageID);
                writer.Write(index.Options.Unique);
                writer.Write(index.Options.IgnoreCase);
                writer.Write(index.Options.TrimWhitespace);
                writer.Write(index.Options.EmptyStringToNull);
                writer.Write(index.Options.RemoveAccents);
            }

            // write all document count 8 bytes here
            writer.Write(this.DocumentCount);
        }

        #endregion Read/Write pages

        #region Methods to work with index array

        /// <summary>
        /// Returns first free index slot to be used
        /// </summary>
        public CollectionIndex GetFreeIndex()
        {
            for (byte i = 0; i < this.Indexes.Length; i++)
            {
                if (this.Indexes[i].IsEmpty) return this.Indexes[i];
            }

            throw LiteException.IndexLimitExceeded(this.CollectionName);
        }

        /// <summary>
        /// Get index from field name (index field name is case sensitive) - returns null if not found
        /// </summary>
        public CollectionIndex GetIndex(string field)
        {
            return this.Indexes.FirstOrDefault(x => x.Field == field);
        }

        /// <summary>
        /// Get primary key index (_id index)
        /// </summary>
        public CollectionIndex PK { get { return this.Indexes[0]; } }

        /// <summary>
        /// Returns all used indexes
        /// </summary>
        public IEnumerable<CollectionIndex> GetIndexes(bool includePK)
        {
            return this.Indexes.Where(x => x.IsEmpty == false && x.Slot >= (includePK ? 0 : 1));
        }

        #endregion Methods to work with index array
    }
}