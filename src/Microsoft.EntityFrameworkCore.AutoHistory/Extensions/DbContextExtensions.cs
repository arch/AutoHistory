// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            NullValueHandling = NullValueHandling.Include
        });

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        public static void EnsureAutoHistory(this DbContext context)
        {
            // Currently, only support Modified and Deleted entity.
            var entries = context.ChangeTracker.Entries().Where(e => !Object.ReferenceEquals(e.Entity.GetType(), new AutoHistory().GetType())
                    && (e.State == EntityState.Modified || e.State == EntityState.Deleted)).ToArray();
            foreach (var entry in entries)
            {
                context.Add(entry.AutoHistory(context));
            }
        }

        internal static AutoHistory AutoHistory(this EntityEntry entry, DbContext context)
        {
            __DbHistory = context.Set<AutoHistory>();
            var history = new AutoHistory
            {
                TableName = entry.Metadata.Relational().TableName,
                EntityName = entry.Entity.GetType().AssemblyQualifiedName
            };
            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var json = new JObject();
            switch (entry.State)
            {
                case EntityState.Added:
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
                        if(prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            bef[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, _jsonerializer)
                            : JValue.CreateNull();

                            aft[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, _jsonerializer)
                            : JValue.CreateNull();
                        }

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
        /// <summary>
        /// Ensures the automatic history rollback.
        /// Must report 2 changes, the entity goes back to its previous state and its last history if change removed
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="entity">The entity being restaured to its previous state.</param>
        /// <param name="Id">The primary key value of the entity.</param>

        public static int Rollback(this DbContext context, object entity, object Id)
        {
            int itemsChanged = 0;
            __DbHistory = context.Set<AutoHistory>();
            if(context.Entry(entity).State == EntityState.Detached)
                return 0;
            AutoHistory history = __DbHistory.Where(h => h.RowId ==  Id.ToString()).OrderByDescending(h => h.Id).FirstOrDefault();
            if (history != null)
            {
                try
                {
                    // If this entity type does not exist in its assembly, GetType throws a TypeLoadException.
                    Type elementType = Type.GetType(history.EntityName, true);
                    JObject jsonObj = JObject.Parse(history.Changed);
                    JToken token = jsonObj.GetValue("before");
                    context.Entry(entity).State = EntityState.Detached;
                    entity = Newtonsoft.Json.JsonConvert.DeserializeObject(token.ToString(),elementType);
                    context.Remove(history);
                    context.Entry(entity).State = EntityState.Modified;
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
                catch(NullReferenceException )
                {
                    //Do nothing
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
