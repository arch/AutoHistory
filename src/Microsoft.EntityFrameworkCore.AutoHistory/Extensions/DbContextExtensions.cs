// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class DbContextExtensions
    {
        private static DbSet<AutoHistory> __DbHistory;
        public static DbSet<AutoHistory> DbHistory(this DbContext context)
        {
            if (__DbHistory == null)
                __DbHistory = context.Set<AutoHistory>();
            return __DbHistory;
        }
        private static JsonSerializer _jsonerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        });

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        public static void EnsureAutoHistory(this DbContext context)
        {
            __DbHistory = context.Set<AutoHistory>();

            // Currently, only support Modified and Deleted entity.
            var entries = context.ChangeTracker.Entries().Where(e => !Object.ReferenceEquals(e.Entity.GetType(), new Microsoft.EntityFrameworkCore.AutoHistory().GetType())
                    && (e.State == EntityState.Modified || e.State == EntityState.Deleted)).ToArray();
            foreach (var entry in entries)
            {
                context.Add(entry.AutoHistory());
            }
        }

        internal static AutoHistory AutoHistory(this EntityEntry entry)
        {

            var history = new AutoHistory
            {
                TableName = entry.Metadata.Relational().TableName,
                EntityName = entry.Entity.GetType().FullName
            };
            AutoHistory parent;

            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var json = new JObject();
            switch (entry.State)
            {
                case EntityState.Added:
                    history.ParentId = null;
                    foreach (var prop in properties)
                    {
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            continue;
                        }
                        json[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, _jsonerializer)
                            : JValue.CreateNull();
                    }

                    // REVIEW: what's the best way to set the RowId?
                    history.RowId = "0";
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Modified:
                    var bef = new JObject();
                    var aft = new JObject();

                    foreach (var prop in properties)
                    {
                        if (prop.IsModified)
                        {
                            bef[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, _jsonerializer)
                            : JValue.CreateNull();

                            aft[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, _jsonerializer)
                            : JValue.CreateNull();
                        }
                    }

                    json["before"] = bef;
                    json["after"] = aft;
                    parent = __DbHistory.Where(h => h.RowId == entry.PrimaryKey()).OrderByDescending(x => x.ParentId).FirstOrDefault();
                    if (parent != null)
                    {
                        history.ParentId = parent.Id;
                    }

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Deleted:
                    foreach (var prop in properties)
                    {
                        json[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, _jsonerializer)
                            : JValue.CreateNull();
                    }

                    parent = __DbHistory.Where(h => h.RowId == entry.PrimaryKey()).OrderByDescending(x => x.ParentId).FirstOrDefault();
                    if (!parent.Equals(null))
                    {
                        history.ParentId = parent.Id;
                    }
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Deleted;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    throw new NotSupportedException("AutoHistory only support Deleted and Modified entity.");
            }

            return history;
        }

        public static int Rollback(this DbContext context, int historyNumber)
        {
            int itemsChanged = 0;
            __DbHistory = context.Set<AutoHistory>();
            AutoHistory h = __DbHistory.Find(historyNumber);
            
            if (h != null && h.ParentId != null)
            {
                try
                {
                    // If this entity type does not exist in this assembly, GetType throws a TypeLoadException.
                    //Type elementType = Type.GetType(h.EntityName, true);
                    //object obj = Activator.CreateInstance(elementType);
                    JObject jsonObj = JObject.Parse(h.Changed);
                    
                    object obj = jsonObj["bef"].ToObject(Type.GetType(h.EntityName, true));
                    context.Entry(obj).State = EntityState.Modified;
                    itemsChanged = context.SaveChanges();
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    Console.WriteLine("{0}: Unable to load json string", ex.Message);

                }
                catch (TypeLoadException e)
                {
                    Console.WriteLine("{0}: Unable to load type", e.GetType().Name);
                }
            }

            return itemsChanged;


        }

        private static string PrimaryKey(this EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();

            var values = new List<object>();
            foreach (var property in key.Properties)
            {
                var value = entry.Property(property.Name).CurrentValue;
                if (value != null)
                {
                    values.Add(value);
                }
            }

            return string.Join(",", values);
        }
    }
}
