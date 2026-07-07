using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace UPBot.UPBot_Code;

public class Database
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    private static readonly Dictionary<Type, string> storagePaths = [];
    private static string storageDirectory;

    private static void LogDatabase(string message)
    {
        Console.WriteLine("[JSON] " + message);
        try
        {
            Utils.Log("[JSON] " + message, null);
        }
        catch
        {
            // Ignore logging failures so persistence still works.
        }
    }

    private static string ResolveStorageDirectory()
    {
        var configuredPath = Environment.GetEnvironmentVariable("UPBOT_DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (Path.HasExtension(fullPath))
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory)) return directory;
            }
            return fullPath;
        }

        var projectRoot = FindProjectRoot();
        return Path.Combine(projectRoot, "Database");
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UPBot.csproj")) || File.Exists(Path.Combine(current.FullName, "UPBot.sln")) || Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string GetStoragePath(Type type)
    {
        if (storagePaths.TryGetValue(type, out var path)) return path;
        return Path.Combine(storageDirectory, type.Name + ".json");
    }

    private static List<PersistedRecord> LoadRecords(Type type)
    {
        var path = GetStoragePath(type);
        if (!File.Exists(path)) return [];

        try
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content)) return [];
            var records = JsonSerializer.Deserialize<List<PersistedRecord>>(content, JsonOptions);
            return records ?? [];
        }
        catch (Exception ex)
        {
            LogDatabase("Failed to read records for " + type.Name + ": " + ex.Message);
            return [];
        }
    }

    private static void SaveRecords(Type type, List<PersistedRecord> records)
    {
        var path = GetStoragePath(type);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? storageDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(records, JsonOptions));
    }

    private static PersistedRecord SerializeObject(object value)
    {
        var record = new PersistedRecord();
        var type = value.GetType();
        foreach (FieldInfo field in GetPersistedFields(type))
        {
            object? fieldValue = field.GetValue(value);
            if (fieldValue == null)
            {
                record.Values[field.Name] = JsonSerializer.SerializeToElement((object?)null, JsonOptions);
            }
            else
            {
                record.Values[field.Name] = JsonSerializer.SerializeToElement(fieldValue, field.FieldType, JsonOptions);
            }
        }
        return record;
    }

    private static T DeserializeObject<T>(PersistedRecord record)
    {
        T result = (T)Activator.CreateInstance(typeof(T))!;
        foreach (FieldInfo field in GetPersistedFields(typeof(T)))
        {
            if (record.Values.TryGetValue(field.Name, out var element))
            {
                if (element.ValueKind == JsonValueKind.Null)
                {
                    field.SetValue(result, null);
                }
                else
                {
                    field.SetValue(result, element.Deserialize(field.FieldType, JsonOptions));
                }
            }
        }
        return result;
    }

    private static List<FieldInfo> GetPersistedFields(Type type)
    {
        List<FieldInfo> fields = [];
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            bool ignore = false;
            foreach (var attribute in field.GetCustomAttributes())
            {
                if (attribute is Entity.NotPersistent)
                {
                    ignore = true;
                    break;
                }
            }
            if (!ignore) fields.Add(field);
        }
        return fields;
    }

    private static List<FieldInfo> GetKeyFields(Type type)
    {
        List<FieldInfo> fields = [];
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var attribute in field.GetCustomAttributes())
            {
                if (attribute is Entity.Key)
                {
                    fields.Add(field);
                    break;
                }
            }
        }
        return fields;
    }

    private static bool RecordMatches(object record, object candidate, List<FieldInfo> keyFields)
    {
        foreach (FieldInfo field in keyFields)
        {
            object? left = field.GetValue(record);
            object? right = field.GetValue(candidate);
            if (!Equals(left, right)) return false;
        }
        return true;
    }

    public static void InitDb(List<Type> tables)
    {
        try
        {
            storageDirectory = ResolveStorageDirectory();
            Directory.CreateDirectory(storageDirectory);

            LogDatabase("Using JSON storage directory: " + storageDirectory);

            foreach (Type tableType in tables)
            {
                if (!typeof(Entity).IsAssignableFrom(tableType))
                    throw new Exception("The class " + tableType + " does not derive from Entity and cannot be used as database table!");

                var path = Path.Combine(storageDirectory, tableType.Name + ".json");
                storagePaths[tableType] = path;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? storageDirectory);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "[]");
                    LogDatabase("Created JSON store for " + tableType.Name + " at " + path);
                }
                else
                {
                    LogDatabase("Using existing JSON store for " + tableType.Name + " at " + path);
                }
            }

            LogDatabase("JSON persistence initialization complete.");
        }
        catch (Exception ex)
        {
            throw new Exception("Cannot initialize JSON database: " + ex.Message);
        }
    }

    public static int Count<T>()
    {
        return GetAll<T>().Count;
    }

    public static void Update<T>(T val)
    {
        Add(val);
    }

    public static void Insert<T>(T val)
    {
        Add(val);
    }

    public static void Add<T>(T val)
    {
        try
        {
            Type type = typeof(T);
            var records = LoadRecords(type);
            var keyFields = GetKeyFields(type);
            int index = -1;

            for (int i = 0; i < records.Count; i++)
            {
                if (RecordMatches(DeserializeObject<T>(records[i]), val, keyFields))
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                records[index] = SerializeObject(val);
                LogDatabase("Updated row for " + type.Name + " in JSON store.");
            }
            else
            {
                records.Add(SerializeObject(val));
                LogDatabase("Inserted row for " + type.Name + " into JSON store.");
            }

            SaveRecords(type, records);
        }
        catch (Exception ex)
        {
            Utils.Log("Error in Adding data for " + typeof(T) + ": " + ex.Message, null);
        }
    }

    public static void Delete<T>(T val)
    {
        try
        {
            Type type = typeof(T);
            var records = LoadRecords(type);
            var keyFields = GetKeyFields(type);
            records.RemoveAll(record => RecordMatches(DeserializeObject<T>(record), val, keyFields));
            SaveRecords(type, records);
            LogDatabase("Deleted row for " + type.Name + " from JSON store.");
        }
        catch (Exception ex)
        {
            Utils.Log("Error in Deleting data for " + typeof(T) + ": " + ex.Message, null);
        }
    }

    public static void DeleteByKeys<T>(params object[] keys)
    {
        try
        {
            Type type = typeof(T);
            var records = LoadRecords(type);
            var keyFields = GetKeyFields(type);
            if (keyFields.Count != keys.Length) throw new Exception("Inconsistent number of keys for: " + typeof(T).FullName);

            for (int i = 0; i < keyFields.Count; i++)
            {
                object? keyValue = keyFields[i].GetValue(Activator.CreateInstance(type)!);
                _ = keyValue;
            }

            records.RemoveAll(record =>
            {
                var obj = DeserializeObject<T>(record);
                for (int i = 0; i < keyFields.Count; i++)
                {
                    object? actual = keyFields[i].GetValue(obj);
                    if (!Equals(actual, keys[i])) return false;
                }
                return true;
            });

            SaveRecords(type, records);
            LogDatabase("Deleted row(s) for " + type.Name + " from JSON store.");
        }
        catch (Exception ex)
        {
            Utils.Log("Error in Deleting data for " + typeof(T) + ": " + ex.Message, null);
        }
    }

    public static T GetByKey<T>(params object[] keys)
    {
        try
        {
            Type type = typeof(T);
            var records = LoadRecords(type);
            var keyFields = GetKeyFields(type);
            if (keyFields.Count != keys.Length) throw new Exception("Inconsistent number of keys for: " + typeof(T).FullName);

            foreach (var record in records)
            {
                var obj = DeserializeObject<T>(record);
                bool matched = true;
                for (int i = 0; i < keyFields.Count; i++)
                {
                    object? actual = keyFields[i].GetValue(obj);
                    if (!Equals(actual, keys[i]))
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return obj;
            }
        }
        catch (Exception ex)
        {
            Utils.Log("Error in Getting data for " + typeof(T) + ": " + ex.Message, null);
        }
        return default!;
    }

    public static List<T> GetAll<T>()
    {
        try
        {
            Type type = typeof(T);
            var records = LoadRecords(type);
            List<T> result = [];
            foreach (var record in records) result.Add(DeserializeObject<T>(record));
            LogDatabase("Loaded " + result.Count + " rows for " + type.Name + " from JSON store.");
            return result;
        }
        catch (Exception ex)
        {
            Utils.Log(" " + typeof(T) + ": " + ex.Message, null);
        }
        return [];
    }

    private sealed class PersistedRecord
    {
        public Dictionary<string, JsonElement> Values { get; set; } = [];
    }
}
