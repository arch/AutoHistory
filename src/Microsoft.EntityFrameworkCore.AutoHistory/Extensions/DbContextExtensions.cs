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
        public static DbSet<AutoHistory> DbHistory(this DbContext context)
        {
            return context.Set<AutoHistory>();
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
                context.Add(entry.AutoHistory());
            }
        }

        internal static AutoHistory AutoHistory(this EntityEntry entry)
        {
            var history = new AutoHistory
            {
                TableName = entry.Metadata.Relational().TableName,
                EntityName = entry.Metadata.Name //entry.Entity.GetType().AssemblyQualifiedName
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
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            bef[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, _jsonerializer)
                            : JValue.CreateNull();

                            aft[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, _jsonerializer)
                            : JValue.CreateNull();
                        }

                        else
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
        /// The entity goes back to a defined previous state or its last previous state.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="entity">The entity being restaured to its previous state.</param>
        /// <param name="historyNumber">[Optional] The history number from where rollback is done.</param>

        public static void AutoHistoryRollback<T>(this DbContext context,  ref T entity, int? historyNumber = null) where T: class
        {
            if(String.IsNullOrEmpty(context.Entry(entity).PrimaryKey()))
                throw new ArgumentNullException(String.Format("The entity {0} doesn't have a valid primary key, so can not be rollbacked", entity.GetType().Name));
            
            if (context.Entry(entity).State == EntityState.Added)
                throw new Exception(String.Format("The entity {0} is added, it can not be rollbacked", entity.GetType().Name));
            
            context.Entry(entity).State = EntityState.Detached;
            string keyString = context.Entry(entity).PrimaryKey();
            var historicChanges = context.Set<AutoHistory>().Where(h => h.RowId == keyString);
            AutoHistory history = null;


            if (historicChanges.Count() == 1)
                return;

            if (historyNumber != null)
            {
                history = historicChanges.Where(h => h.Id == historyNumber).FirstOrDefault();
            }
            else
            {
                history = historicChanges.OrderByDescending(h => h.Id).FirstOrDefault();
            }

            if (history != null)
            {
                try
                {
                    // If this entity type does not exist in its assembly, GetType throws a TypeLoadException.
                    //Type elementType = Type.GetType(history.EntityName, true);
                    JObject jsonObj = JObject.Parse(history.Changed);                    
                    entity = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonObj.GetValue("before").ToString());
                    context.Remove(history);
                    context.Entry(entity).State = EntityState.Modified;
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
