﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Vanara.Extensions;

namespace Vanara.InteropServices
{
	/// <summary>Functions to safely convert a memory pointer to a type.</summary>
	public static class IntPtrConverter
	{
		/// <summary>Converts the specified pointer to <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The destination type.</typeparam>
		/// <param name="ptr">The pointer to a block of memory.</param>
		/// <param name="sz">The size of the allocated memory block.</param>
		/// <returns>A value of the type specified.</returns>
		public static T Convert<T>(this IntPtr ptr, uint sz) => (T)Convert(ptr, sz, typeof(T));

		/// <summary>Converts the specified pointer to type specified in <paramref name="destType"/>.</summary>
		/// <param name="ptr">The pointer to a block of memory.</param>
		/// <param name="sz">The size of the allocated memory block.</param>
		/// <param name="destType">The destination type.</param>
		/// <returns>A value of the type specified.</returns>
		/// <exception cref="NotSupportedException">Thrown if type cannot be converted from memory.</exception>
		public static object Convert(this IntPtr ptr, uint sz, Type destType)
		{
			if (ptr == IntPtr.Zero) throw new ArgumentException("Cannot convert a null pointer.", nameof(ptr));
			if (sz == 0) throw new ArgumentException("Cannot convert a pointer with no Size.", nameof(sz));

			// Handle byte array as special case
			if (destType.IsArray && destType.GetElementType() == typeof(byte))
				return ptr.ToArray<byte>((int)sz);

			var typeCode = Type.GetTypeCode(destType);
			switch (typeCode)
			{
				case TypeCode.Object:
					if (typeof(ISerializable).IsAssignableFrom(destType))
					{
						using (var mem = new MemoryStream(ptr.ToArray<byte>((int)sz)))
							return new BinaryFormatter().Deserialize(mem);
					}
					try
					{
						return GetBlittable(destType);
					}
					catch (ArgumentOutOfRangeException e)
					{
						throw e;
					}
					catch
					{
						throw new NotSupportedException("Unsupported type parameter.");
					}
				case TypeCode.Boolean:
					object boolVal;
					if (sz == 1)
						boolVal = GetBlittable(typeof(byte));
					else if (sz == 2)
						boolVal = GetBlittable(typeof(ushort));
					else if (sz == 4)
						boolVal = GetBlittable(typeof(uint));
					else if (sz == 8)
						boolVal = GetBlittable(typeof(ulong));
					else
						throw SizeExc();
					return System.Convert.ChangeType(boolVal, typeCode);

				case TypeCode.Char:
					return System.Convert.ChangeType(GetBlittable(typeof(ushort)), typeCode);

				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					return GetBlittable(destType);

				case TypeCode.DateTime:
					return DateTime.FromBinary((long)GetBlittable(typeof(long)));

				default:
					throw new NotSupportedException("Unsupported type parameter.");
			}

			object GetBlittable(Type retType)
			{
				if (Marshal.SizeOf(retType) <= sz)
					return Marshal.PtrToStructure(ptr, retType);
				throw SizeExc();
			}

			Exception SizeExc() => new ArgumentOutOfRangeException(nameof(sz), "Type size is larger than buffer size.");
		}

		/// <summary>Converts the specified pointer to <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The destination type.</typeparam>
		/// <param name="hMem">A block of allocated memory.</param>
		/// <returns>A value of the type specified.</returns>
		public static T ToType<T>(this SafeAllocatedMemoryHandle hMem)
		{
			if (hMem == null) throw new ArgumentNullException(nameof(hMem));
			return Convert<T>(hMem.DangerousGetHandle(), (uint)hMem.Size);
		}
	}
}