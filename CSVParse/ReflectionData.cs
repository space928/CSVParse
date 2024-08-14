using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace CSVParse;

/// <summary>
/// Provides an efficient representation of the fields and properties of a data structure and provides efficient 
/// methods for getting and setting their values.
/// </summary>
/// <typeparam name="T">The type of data structure to collect metadata for.</typeparam>
internal struct ReflectionData<T> where T : new()
{
    public string fieldName;
    public string? csvName;
    public int index;
    public Type fieldType;
    public bool isNullable;
    public ICustomCSVSerializer? customDeserializer;
    public GetValue getValueFunc;
    public SetValue setValueFunc;
    public SerializeValue serializeValueFunc;
    public DeserializeValue deserializeValueFunc;

    internal delegate void SetValue(ref T target, object? value);
    internal delegate object? GetValue(ref T target);
    internal delegate void DeserializeValue(ref T target, ReadOnlySpan<char> value);
    internal delegate int SerializeValue(ref T target, Span<char> dst);

    internal delegate int SerializeFunc(Span<char> dst);

    private delegate void SetValueGeneric<U>(ref T target, U value);
    private delegate U GetValueGeneric<U>(ref T target);
    private delegate void Format<U>(U val, Span<char> dst, out int written);

    private static bool UseILGeneration => RuntimeFeature.IsDynamicCodeSupported;

    public static ReflectionData<T>? Create(FieldInfo field, int defaultIndex = -1)
    {
        return Create(null, field, defaultIndex);
    }

    public static ReflectionData<T>? Create(PropertyInfo prop, int defaultIndex = -1)
    {
        return Create(prop, null, defaultIndex);
    }

    public static ReflectionData<T>? Create(PropertyInfo? prop, FieldInfo? field, int defaultIndex)
    {
        MemberInfo member;
        Type ftype;
        if (prop != null)
        {
            member = prop;
            ftype = prop.PropertyType;
        }
        else if (field != null)
        {
            member = field;
            ftype = field.FieldType;
        }
        else
        {
            throw new NullReferenceException();
        }

        bool skip = false;
        string? csvName = null;
        int index = -1;
        ICustomCSVSerializer? customSerializer = null;
        foreach (var attr in member.GetCustomAttributes())
        {
            switch (attr)
            {
                case CSVSkipAttribute:
                    skip = true;
                    break;
                case CSVNameAttribute name:
                    csvName = name.Name;
                    break;
                //case CSVCustomSerializerAttribute<ICustomCSVSerializer> serializer:
                //    customSerializer = Activator.CreateInstance(serializer.GetType().GenericTypeArguments[0]) as ICustomCSVSerializer;
                //    break;
                case CSVIndexAttribute indexAttr:
                    index = indexAttr.Index;
                    break;
                default:
                    var typ = attr.GetType();
                    if (typ.IsGenericType && typ.GetGenericTypeDefinition() == typeof(CSVCustomSerializerAttribute<>))
                    {
                        customSerializer = Activator.CreateInstance(typ.GenericTypeArguments[0]) as ICustomCSVSerializer;
                    }
                    break;
            }
            if (skip)
                break;
        }
        if (skip)
            return null;

        bool nullable = false;
        if (Nullable.GetUnderlyingType(ftype) is Type t)
        {
            nullable = true;
            ftype = t;
        }

        GetValue getValue;
        SetValue setValue;
        SerializeValue serializeValue;
        DeserializeValue deserializeValue;
        if (prop != null)
        {
            getValue = MakeGetter(prop);
            setValue = MakeSetter(prop);
            if (customSerializer != null)
            {
                serializeValue = (ref T target, Span<char> dst) => 0;
                deserializeValue = (ref T target, ReadOnlySpan<char> value) => { };
            }
            else
            {
                serializeValue = MakeSerializer(prop);
                deserializeValue = MakeDeserializer(prop);
            }
        }
        else if (field != null)
        {
            getValue = MakeGetter(field);
            setValue = MakeSetter(field);
            if (customSerializer != null)
            {
                serializeValue = (ref T target, Span<char> dst) => 0;
                deserializeValue = (ref T target, ReadOnlySpan<char> value) => { };
            }
            else
            {
                serializeValue = MakeSerializer(field);
                deserializeValue = MakeDeserializer(field);
            }
        }
        else
        {
            throw new NullReferenceException();
        }

        return new()
        {
            fieldName = member.Name,
            csvName = csvName,
            index = index == -1 ? defaultIndex : index,
            customDeserializer = customSerializer,
            isNullable = nullable,
            fieldType = ftype,
            getValueFunc = getValue,
            setValueFunc = setValue,
            serializeValueFunc = serializeValue,
            deserializeValueFunc = deserializeValue
        };
    }

    private static GetValue MakeGetter(PropertyInfo pi)
    {
        //return pi.GetGetMethod()?.CreateDelegate<Func<>>() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
        if (UseILGeneration)
        {
            var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynGetter = new($"get_{pi.Name}_{pi.GetHashCode()}", typeof(object), [typeof(T).MakeByRefType()], typeof(T), true);
            var il = dynGetter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (typeof(T).IsValueType)
            {
                il.Emit(OpCodes.Call, getter);
            }
            else
            {
                il.Emit(OpCodes.Ldind_Ref);
                il.Emit(OpCodes.Callvirt, getter);
            }
            if (pi.PropertyType.IsValueType)
                il.Emit(OpCodes.Box);
            il.Emit(OpCodes.Ret);
            return dynGetter.CreateDelegate<GetValue>();
        }
        else
        {
            return (ref T target) => pi.GetValue(target);
        }
    }

    private static GetValue MakeGetter(FieldInfo fi)
    {
        if (UseILGeneration)
        {
            DynamicMethod setter = new($"get_{fi.Name}_{fi.GetHashCode()}", typeof(object), [typeof(T).MakeByRefType()], typeof(T), true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Ldfld, fi);
            if (fi.FieldType.IsValueType)
                il.Emit(OpCodes.Box);
            il.Emit(OpCodes.Ret);
            return setter.CreateDelegate<GetValue>();
        }
        else
        {
            return (ref T target) =>
            {
                var tref = __makeref(target);
                return fi.GetValueDirect(tref);
            };
        }
    }

    private static GetValueGeneric<U> MakeGetter<U>(PropertyInfo pi)
    {
        if (UseILGeneration)
        {
            var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynGetter = new($"get_{pi.Name}_{pi.GetHashCode()}", typeof(U), [typeof(T).MakeByRefType()], typeof(T), true);
            var il = dynGetter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (typeof(T).IsValueType)
            {
                il.Emit(OpCodes.Call, getter);
            }
            else
            {
                il.Emit(OpCodes.Ldind_Ref);
                il.Emit(OpCodes.Callvirt, getter);
            }
            il.Emit(OpCodes.Ret);
            return dynGetter.CreateDelegate<GetValueGeneric<U>>();
        }
        else
        {
            /*var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            var get = getter.CreateDelegate<GetValueGeneric<U>>();
            return (ref T target) => get(target);*/
            return (ref T target) => (U)pi.GetValue(target)!;
        }
    }

    private static GetValueGeneric<U> MakeGetter<U>(FieldInfo fi)
    {
        if (UseILGeneration)
        {
            DynamicMethod setter = new($"get_{fi.Name}_{fi.GetHashCode()}", typeof(U), [typeof(T).MakeByRefType()], typeof(T), true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Ldfld, fi);
            il.Emit(OpCodes.Ret);
            return setter.CreateDelegate<GetValueGeneric<U>>();
        }
        else
        {
            return (ref T target) =>
            {
                var tref = __makeref(target);
                return (U)fi.GetValueDirect(tref)!;
            };
        }
    }

    private static SetValue MakeSetter(PropertyInfo pi)
    {
        if (UseILGeneration)
        {
            var setter = pi.GetSetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynSetter = new($"set_{pi.Name}_{pi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(object)], typeof(T), true);
            var il = dynSetter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (pi.PropertyType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, pi.PropertyType);
            if (typeof(T).IsValueType)
                il.Emit(OpCodes.Call, setter);
            else
                il.Emit(OpCodes.Callvirt, setter);
            il.Emit(OpCodes.Ret);
            return dynSetter.CreateDelegate<SetValue>();
        }
        else
        {
            /*var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            var get = getter.CreateDelegate<GetValueGeneric<U>>();
            return (ref T target) => get(target);*/
            return (ref T obj, object? val) =>
            {
                if (obj != null)
                {
                    object box = obj;
                    pi.SetValue(obj, val);
                    obj = (T)box;
                }
            };
        }
    }

    private static SetValue MakeSetter(FieldInfo fi)
    {
        if (UseILGeneration)
        {
            DynamicMethod setter = new($"set_{fi.Name}_{fi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(object)], typeof(T), true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Unbox_Any, fi.FieldType);
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Stfld, fi);
            il.Emit(OpCodes.Ret);
            return setter.CreateDelegate<SetValue>();
        }
        else
        {
            return (ref T target, object? value) =>
            {
                var tref = __makeref(target);
                fi.SetValueDirect(tref, value!);
            };
        }
    }

    private static SetValueGeneric<U> MakeSetter<U>(PropertyInfo pi)
    {
        if (UseILGeneration)
        {
            var setter = pi.GetSetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynSetter = new($"set_{pi.Name}_{pi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(U)], typeof(T), true);
            var il = dynSetter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (pi.PropertyType != typeof(U) && !pi.PropertyType.IsEnum)
                il.Emit(OpCodes.Newobj, pi.PropertyType.GetConstructor([typeof(U)])
                    ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} must have a constructor which takes a single {typeof(U).Name}!"));
            if (typeof(T).IsValueType)
                il.Emit(OpCodes.Call, setter);
            else
                il.Emit(OpCodes.Callvirt, setter);
            il.Emit(OpCodes.Ret);
            return dynSetter.CreateDelegate<SetValueGeneric<U>>();
        }
        else
        {
            /*var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            var get = getter.CreateDelegate<GetValueGeneric<U>>();
            return (ref T target) => get(target);*/
            return (ref T target, U val) =>
            {
                if (target != null)
                {
                    object box = target;
                    pi.SetValue(target, val);
                    target = (T)box; // Booo, unboxing...
                }
            };
        }
    }

    private static SetValueGeneric<U> MakeSetter<U>(FieldInfo fi)
    {
        if (UseILGeneration)
        {
            DynamicMethod setter = new($"set_{fi.Name}_{fi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(U)], typeof(T)!, true);
            
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (fi.FieldType != typeof(U) && !fi.FieldType.IsEnum)
                il.Emit(OpCodes.Newobj, fi.FieldType.GetConstructor([typeof(U)])
                    ?? throw new CSVSerializerException($"Field '{fi.Name}' of type {fi.FieldType.Name} must have a constructor which takes a single {typeof(U).Name}!"));
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Stfld, fi);
            il.Emit(OpCodes.Ret);

            return setter.CreateDelegate<SetValueGeneric<U>>();
        }
        else
        {
            return (ref T target, U value) =>
            {
                var tref = __makeref(target);
                fi.SetValueDirect(tref, value!);
            };
        }
    }

    private static DeserializeValue MakeConstructorDeserializer(PropertyInfo pi, ConstructorInfo ctor)
    {
        if (UseILGeneration)
        {
            var setter = pi.GetSetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynSetter = new($"set_{pi.Name}_{pi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(ReadOnlySpan<char>)], typeof(T), true);

            var il = dynSetter.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, ctor);
            if (typeof(T).IsValueType)
                il.Emit(OpCodes.Call, setter);
            else
                il.Emit(OpCodes.Callvirt, setter);
            il.Emit(OpCodes.Ret);
            return dynSetter.CreateDelegate<DeserializeValue>();
        }
        else
        {
            // WARNING: This code path is super slow! 
            return (ref T target, ReadOnlySpan<char> str) =>
            {
                if (target != null)
                {
                    object box = target;
                    pi.SetValue(target, ctor.Invoke([new string(str)]));
                    target = (T)box; // Booo, unboxing...
                }
            };
        }
    }

    private static DeserializeValue MakeConstructorDeserializer(FieldInfo fi, ConstructorInfo ctor)
    {
        if (UseILGeneration)
        {
            DynamicMethod setter = new($"set_{fi.Name}_{fi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(ReadOnlySpan<char>)], typeof(T)!, true);

            var il = setter.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, ctor);
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Stfld, fi);
            il.Emit(OpCodes.Ret);

            return setter.CreateDelegate<DeserializeValue>();
        }
        else
        {
            // WARNING: This code path is super slow! 
            return (ref T target, ReadOnlySpan<char> str) =>
            {
                var tref = __makeref(target);
                fi.SetValueDirect(tref, ctor.Invoke([new string(str)])!);
            };
        }
    }

    private static DeserializeValue MakeDeserializer(PropertyInfo prop)
    {
        var type = prop.PropertyType;
        if (Nullable.GetUnderlyingType(type) is Type t)
            type = t;
        var simpleType = type;
        if (type.IsEnum)
            simpleType = type.GetEnumUnderlyingType();

        if (simpleType == typeof(bool))
        {
            var setterResolved = MakeSetter<bool>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, (str.Length == 1 && str[0] == '1') || str.SequenceEqual("true"));
        }
        else if (simpleType == typeof(byte))
        {
            var setterResolved = MakeSetter<byte>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, byte.Parse(str));
        }
        else if (simpleType == typeof(sbyte))
        {
            var setterResolved = MakeSetter<sbyte>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, sbyte.Parse(str));
        }
        else if (simpleType == typeof(char))
        {
            var setterResolved = MakeSetter<char>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, str[0]);
        }
        else if (simpleType == typeof(decimal))
        {
            var setterResolved = MakeSetter<decimal>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, decimal.Parse(str));
        }
        else if (simpleType == typeof(double))
        {
            var setterResolved = MakeSetter<double>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, double.Parse(str));
        }
        else if (simpleType == typeof(float))
        {
            var setterResolved = MakeSetter<float>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, float.Parse(str));
        }
        else if (simpleType == typeof(int))
        {
            var setterResolved = MakeSetter<int>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, int.Parse(str));
        }
        else if (simpleType == typeof(uint))
        {
            var setterResolved = MakeSetter<uint>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, uint.Parse(str));
        }
        else if (simpleType == typeof(nint))
        {
            var setterResolved = MakeSetter<nint>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, nint.Parse(str));
        }
        else if (simpleType == typeof(long))
        {
            var setterResolved = MakeSetter<long>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, long.Parse(str));
        }
        else if (simpleType == typeof(ulong))
        {
            var setterResolved = MakeSetter<ulong>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, ulong.Parse(str));
        }
        else if (simpleType == typeof(short))
        {
            var setterResolved = MakeSetter<short>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, short.Parse(str));
        }
        else if (simpleType == typeof(ushort))
        {
            var setterResolved = MakeSetter<ushort>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, ushort.Parse(str));
        }
        else if (simpleType == typeof(string))
        {
            var setterResolved = MakeSetter<string>(prop);
            return (ref T t, ReadOnlySpan<char> str) => setterResolved(ref t, new(str));
        } 
        else if (simpleType == typeof(PreAllocatedString))
        {
            var getterResolved = MakeGetter<PreAllocatedString>(prop);
            var setterResolved = MakeSetter<PreAllocatedString>(prop);
            return (ref T t, ReadOnlySpan<char> str) =>
            {
                var s = getterResolved(ref t);
                s.Update(str);
                setterResolved(ref t, s);
            };
        }
        else if (simpleType.GetConstructor([typeof(ReadOnlySpan<char>)]) is ConstructorInfo ctor)
        {
            // TODO: Should also support ISpanParsable
            return MakeConstructorDeserializer(prop, ctor);
        }
        else
            throw new CSVSerializerException($"Property '{prop.Name}' of type {prop.PropertyType.Name} is not supported!");
    }

    private static DeserializeValue MakeDeserializer(FieldInfo field)
    {
        var type = field.FieldType;
        if (Nullable.GetUnderlyingType(type) is Type t)
            type = t;
        var simpleType = type;
        if (type.IsEnum)
            simpleType = type.GetEnumUnderlyingType();

        if (simpleType == typeof(bool))
        {
            var setter = MakeSetter<bool>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, (str.Length == 1 && str[0] == '1') || str.SequenceEqual("true"));
        }
        else if (simpleType == typeof(byte))
        {
            var setter = MakeSetter<byte>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, byte.Parse(str));
        }
        else if (simpleType == typeof(sbyte))
        {
            var setter = MakeSetter<sbyte>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, sbyte.Parse(str));
        }
        else if (simpleType == typeof(char))
        {
            var setter = MakeSetter<char>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, str[0]);
        }
        else if (simpleType == typeof(decimal))
        {
            var setter = MakeSetter<decimal>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, decimal.Parse(str));
        }
        else if (simpleType == typeof(double))
        {
            var setter = MakeSetter<double>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, double.Parse(str));
        }
        else if (simpleType == typeof(float))
        {
            var setter = MakeSetter<float>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, float.Parse(str));
        }
        else if (simpleType == typeof(int))
        {
            var setter = MakeSetter<int>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, int.Parse(str));
        }
        else if (simpleType == typeof(uint))
        {
            var setter = MakeSetter<uint>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, uint.Parse(str));
        }
        else if (simpleType == typeof(nint))
        {
            var setter = MakeSetter<nint>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, nint.Parse(str));
        }
        else if (simpleType == typeof(long))
        {
            var setter = MakeSetter<long>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, long.Parse(str));
        }
        else if (simpleType == typeof(ulong))
        {
            var setter = MakeSetter<ulong>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, ulong.Parse(str));
        }
        else if (simpleType == typeof(short))
        {
            var setter = MakeSetter<short>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, short.Parse(str));
        }
        else if (simpleType == typeof(ushort))
        {
            var setter = MakeSetter<ushort>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, ushort.Parse(str));
        }
        else if (simpleType == typeof(string))
        {
            var setter = MakeSetter<string>(field);
            return (ref T t, ReadOnlySpan<char> str) => setter(ref t, new(str));
        }
        else if (simpleType == typeof(PreAllocatedString))
        {
            var getterResolved = MakeGetter<PreAllocatedString>(field);
            var setterResolved = MakeSetter<PreAllocatedString>(field);
            return (ref T t, ReadOnlySpan<char> str) =>
            {
                var s = getterResolved(ref t);
                s.Update(str);
                setterResolved(ref t, s);
            };
        }
        else if (simpleType.GetConstructor([typeof(ReadOnlySpan<char>)]) is ConstructorInfo ctor)
        {
            return MakeConstructorDeserializer(field, ctor);
        }
        else
            throw new CSVSerializerException($"Field '{field.Name}' of type {field.FieldType.Name} is not supported!");

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteField<U>(ref T t, nint offset, U val)
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref Unsafe.AddByteOffset(ref t, offset)), val);
        }*/
    }

    private static void FormatBool(bool val, Span<char> dst, out int written)
    {
        if (val)
        {
            if (dst.Length >= 4)
            {
                "true".CopyTo(dst);
                written = 4;
                return;// true;
            }
            written = 0;
            return;// false;
        }
        else
        {
            if (dst.Length >= 5)
            {
                "false".CopyTo(dst);
                written = 5;
                return;// true;
            }
            written = 0;
            return;// false;
        }
    }

    private static SerializeValue MakeSerializer(FieldInfo field)
    {
        var type = field.FieldType;
        if (Nullable.GetUnderlyingType(type) is Type t)
            type = t;
        var simpleType = type;
        if (type.IsEnum)
            simpleType = type.GetEnumUnderlyingType();

        if (simpleType == typeof(bool))
        {
            return MakeSerializerInternal(field, (bool val, Span<char> dst, out int written) => FormatBool(val, dst, out written));//val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(byte))
        {
            return MakeSerializerInternal(field, (byte val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(sbyte))
        {
            return MakeSerializerInternal(field, (sbyte val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(char))
        {
            return MakeSerializerInternal(field, (char val, Span<char> dst, out int written) => { dst[0] = val; written = 1; });
        }
        else if (simpleType == typeof(decimal))
        {
            return MakeSerializerInternal(field, (decimal val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(double))
        {
            return MakeSerializerInternal(field, (double val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(float))
        {
            return MakeSerializerInternal(field, (float val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(int))
        {
            return MakeSerializerInternal(field, (int val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(uint))
        {
            return MakeSerializerInternal(field, (uint val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(nint))
        {
            return MakeSerializerInternal(field, (nint val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(long))
        {
            return MakeSerializerInternal(field, (long val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(ulong))
        {
            return MakeSerializerInternal(field, (ulong val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(short))
        {
            return MakeSerializerInternal(field, (short val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(ushort))
        {
            return MakeSerializerInternal(field, (ushort val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(string))
        {
            return MakeSerializerInternal(field, (string val, Span<char> dst, out int written) => { bool wrote = val.TryCopyTo(dst); written = wrote ? val.Length : 0; });
        }
        else if (simpleType == typeof(PreAllocatedString))
        {
            return MakeSerializerInternal(field, (PreAllocatedString val, Span<char> dst, out int written) => { bool wrote = val.Span.TryCopyTo(dst); written = wrote ? val.Length : 0; });
        }
        else if (simpleType.GetMethod("Serialize", [typeof(Span<char>)]) is MethodInfo serializer && serializer.ReturnType == typeof(int))
        {
            // TODO should also support ISpanFormatable
            //throw new NotImplementedException();
            //var f = serializer.CreateDelegate<SerializeFunc>();
            // TODO: Implement IL generation for fast path...
            return (ref T t, Span<char> dst) =>
            {
                var f = serializer.CreateDelegate<SerializeFunc>(t);
                return f(dst);
            };
        }
        else
            throw new CSVSerializerException($"Field '{field.Name}' of type {field.FieldType.Name} is not supported!");

        static SerializeValue MakeSerializerInternal<U>(FieldInfo field, Format<U> format)
        {
            var getter = MakeGetter<U>(field);
            return (ref T t, Span<char> dst) =>
            {
                var val = getter(ref t);
                format(val, dst, out int written);
                return written;
            };
        }
    }

    private static SerializeValue MakeSerializer(PropertyInfo field)
    {
        var type = field.PropertyType;
        if (Nullable.GetUnderlyingType(type) is Type t)
            type = t;
        var simpleType = type;
        if (type.IsEnum)
            simpleType = type.GetEnumUnderlyingType();

        if (simpleType == typeof(bool))
        {
            return MakeSerializerInternal(field, (bool val, Span<char> dst, out int written) => FormatBool(val, dst, out written));
        }
        else if (simpleType == typeof(byte))
        {
            return MakeSerializerInternal(field, (byte val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(sbyte))
        {
            return MakeSerializerInternal(field, (sbyte val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(char))
        {
            return MakeSerializerInternal(field, (char val, Span<char> dst, out int written) => { dst[0] = val; written = 1; });
        }
        else if (simpleType == typeof(decimal))
        {
            return MakeSerializerInternal(field, (decimal val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(double))
        {
            return MakeSerializerInternal(field, (double val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(float))
        {
            return MakeSerializerInternal(field, (float val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(int))
        {
            return MakeSerializerInternal(field, (int val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(uint))
        {
            return MakeSerializerInternal(field, (uint val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(nint))
        {
            return MakeSerializerInternal(field, (nint val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(long))
        {
            return MakeSerializerInternal(field, (long val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(ulong))
        {
            return MakeSerializerInternal(field, (ulong val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(short))
        {
            return MakeSerializerInternal(field, (short val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(ushort))
        {
            return MakeSerializerInternal(field, (ushort val, Span<char> dst, out int written) => val.TryFormat(dst, out written));
        }
        else if (simpleType == typeof(string))
        {
            return MakeSerializerInternal(field, (string val, Span<char> dst, out int written) => { bool wrote = val.TryCopyTo(dst); written = wrote ? val.Length : 0; });
        }
        else if (simpleType == typeof(PreAllocatedString))
        {
            return MakeSerializerInternal(field, (PreAllocatedString val, Span<char> dst, out int written) => { bool wrote = val.Span.TryCopyTo(dst); written = wrote ? val.Length : 0; });
        }
        else if (simpleType.GetMethod("Serialize", [typeof(Span<char>)]) is MethodInfo serializer && serializer.ReturnType == typeof(int))
        {
            //throw new NotImplementedException();
            //var f = serializer.CreateDelegate<SerializeFunc>();
            // TODO: Implement IL generation for fast path...
            return (ref T t, Span<char> dst) =>
            {
                var f = serializer.CreateDelegate<SerializeFunc>(t);
                return f(dst);
            };
        }
        else
            throw new CSVSerializerException($"Property '{field.Name}' of type {field.PropertyType.Name} is not supported!");

        static SerializeValue MakeSerializerInternal<U>(PropertyInfo field, Format<U> format)
        {
            var getter = MakeGetter<U>(field);
            return (ref T t, Span<char> dst) =>
            {
                var val = getter(ref t);
                format(val, dst, out int written);
                return written;
            };
        }
    }
}
