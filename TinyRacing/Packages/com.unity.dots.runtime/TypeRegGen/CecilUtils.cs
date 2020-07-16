using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Cecil.Awesome;
using FieldTypeTuple = System.Collections.Generic.List<System.Tuple<Mono.Cecil.FieldReference, Mono.Cecil.TypeReference>>;

namespace Unity.ZeroPlayer
{
    public static class CecilUtils
    {
        public static bool HasNamedAttribute(this TypeReference type, string attributeName)
        {
            return CustomAttributesHasAttributeNamed(type.Resolve().CustomAttributes, attributeName);
        }

        public static bool HasNamedAttribute(this FieldReference field, string attributeName)
        {
            return CustomAttributesHasAttributeNamed(field.Resolve().CustomAttributes, attributeName);
        }

        public static void Emit(this ILProcessor il, OpCode opcode, List<FieldReference> fieldPath)
        {
            if (opcode != OpCodes.Ldflda && opcode != OpCodes.Ldfld)
                throw new InvalidProgramException("This extension must be used with OpCodes.Ldflda or OpCodes.Ldfld");

            for (int i = 0; i < fieldPath.Count; ++i)
            {
                if (i != fieldPath.Count - 1)
                    il.Emit(OpCodes.Ldflda, fieldPath[i]);
                else
                    il.Emit(opcode, fieldPath.Last());
            }
        }

        public static bool CustomAttributesHasAttributeNamed(ICollection<CustomAttribute> attributes, string attributeName)
        {
            var fullAttrName = attributeName + "Attribute";
            return attributes.FirstOrDefault(ca =>
                ca.AttributeType.Name == attributeName || ca.AttributeType.Name == fullAttrName)
                != null;
        }

        public static List<TypeReference> CreateGenericArgs(ModuleDefinition module, TypeReference fieldType)
        {
            return CreateGenericArgs(module, (GenericInstanceType)fieldType);
        }

        public static List<TypeReference> CreateGenericArgs(ModuleDefinition module, GenericInstanceType git)
        {
            return git.GenericArguments.Select(t => module.ImportReference(t)).ToList();
        }

        public static MethodReference MakeMethodRefForGenericFieldType(AssemblyDefinition asm, MethodReference method, TypeReference fieldType)
        {
            var methodRef = asm.MainModule.ImportReference(method);
            if (fieldType is GenericInstanceType)
            {
                List<TypeReference> genericArgs = CreateGenericArgs(asm.MainModule, fieldType);
                methodRef = asm.MainModule.ImportReference(method.MakeHostInstanceGeneric(genericArgs.ToArray()));
            }

            return methodRef;
        }

        public static IEnumerable<List<FieldReference>> IterateJobFields(TypeReference type,
            Func<FieldReference, bool> shouldYieldFilter = null,
            Func<FieldReference, bool> shouldRecurseFilter = null)
        {
            var fieldPath = new List<FieldReference>();
            foreach (var item in IterateJobFields(type, shouldYieldFilter, shouldRecurseFilter, fieldPath))
                yield return item;
        }

        static IEnumerable<List<FieldReference>> IterateJobFields(TypeReference type,
            Func<FieldReference, bool> shouldYieldFilter,
            Func<FieldReference, bool> shouldRecurseFilter,
            List<FieldReference> fieldPath)
        {
            // The incoming `type` should be a full generic instance.  This genericResolver
            // will help us resolve the generic parameters of any of its fields
            var genericResolver = TypeResolver.For(type);

            foreach (var typeField in type.Resolve().Fields)
            {
                // Early out the statics: (may add an option to change this later. But see next comment.)
                // Excluding statics covers:
                // 1) enums which infinitely recurse because the values in the enum are of the same enum type
                // 2) statics which infinitely recurse themselves (Such as vector3.zero.zero.zero.zero)
                if (typeField.IsStatic)
                    continue;

                // The fully generic-resolved field reference.  This is the reference that's needed
                // as a Ldfld(a) reference.
                var genericResolvedFieldType = genericResolver.Resolve(typeField.FieldType);

                // TODO this is the point at which generic jobs break down.  We have no
                // visibility into the generic type, so we can't do anything with internal
                // members that are relevant to jobs.  When we don't support generic jobs,
                // this next check can be made a hard error, and the field resolve can happen later.
                // So if there are any generic parameters left, we just ignore this field.
                if (genericResolvedFieldType.ContainsGenericParameter)
                    continue;

                var shouldYield = shouldYieldFilter?.Invoke(typeField) ?? true;
                var shouldRecurse = shouldRecurseFilter?.Invoke(typeField) ?? true;

                if (!shouldYield && !shouldRecurse)
                    continue;

                var genericResolvedField = genericResolver.ResolveFieldType(typeField);
                var f = genericResolver.Resolve(typeField);
                fieldPath.Add(f);

                // yield the current field path
                if (shouldYield)
                    yield return fieldPath;

                // only recurse into things that are straight values; no pointers or byref values
                if (shouldRecurse && typeField.FieldType.IsValueType && !typeField.FieldType.IsPrimitive)
                {
                    // make sure we iterate the FieldType with all generic params resolved
                    foreach (var fr in IterateJobFields(genericResolvedFieldType, shouldYieldFilter, shouldRecurseFilter, fieldPath))
                        yield return fr;
                }

                fieldPath.RemoveAt(fieldPath.Count - 1);
            }
        }

        public static List<FieldReference> ImportReferencesIntoAndMakeFieldPathPublic(this IEnumerable<FieldReference> fieldPath, AssemblyDefinition asm)
        {
            var importedFields = new List<FieldReference>();
            foreach (var field in fieldPath)
            {
                var importedField = field.ImportReferenceInto(asm);
                if (field.DeclaringType.HasGenericParameters)
                {
                    importedField = TypeRegGen.MakeGenericFieldSpecialization(importedField, field.DeclaringType.GenericParameters.ToArray());
                }

                importedFields.Add(importedField);
                field.Resolve().IsPublic = true;
            }

            return importedFields;
        }

        public static TypeReference ImportReferenceInto(this TypeReference typeRef, AssemblyDefinition asm)
        {
            var module = asm.MainModule;
            if (typeRef.IsGenericInstance)
            {
                GenericInstanceType importedType = new GenericInstanceType(module.ImportReference(typeRef.Resolve()));
                GenericInstanceType genericType = typeRef as GenericInstanceType;
                foreach (var ga in genericType.GenericArguments)
                    importedType.GenericArguments.Add(ga.IsGenericParameter ? ga : module.ImportReference(ga));
                return module.ImportReference(importedType);
            }

            return module.ImportReference(typeRef);
        }

        public static MethodReference ImportReferenceInto(this MethodReference methodRef, AssemblyDefinition asm)
        {
            var module = asm.MainModule;
            var declaringType = methodRef.DeclaringType.ImportReferenceInto(asm);
            var returnType = methodRef.ReturnType.ImportReferenceInto(asm);

            var importedMethod = new MethodReference(methodRef.Name, returnType, declaringType);
            foreach (var p in methodRef.Parameters)
            {
                importedMethod.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType.ImportReferenceInto(asm)));
            }

            foreach (var gp in methodRef.GenericParameters)
            {
                importedMethod.GenericParameters.Add(gp);
            }

            return module.ImportReference(importedMethod);
        }

        public static FieldReference ImportReferenceInto(this FieldReference fieldRef, AssemblyDefinition asm)
        {
            var declaringType = fieldRef.DeclaringType.ImportReferenceInto(asm);
            var fieldType = fieldRef.FieldType.ImportReferenceInto(asm);

            var importedField = new FieldReference(fieldRef.Name, fieldType, declaringType);
            return asm.MainModule.ImportReference(importedField);
        }
    }

    // utility until InterfaceGen is merged into the ILPP
    static class ILProcessorExtensions
    {
        public static void InsertAfter(this ILProcessor ilProcessor, Instruction insertAfterThisOne,
            IEnumerable<Instruction> instructions)
        {
            var prev = insertAfterThisOne;
            foreach (var instruction in instructions)
            {
                ilProcessor.InsertAfter(prev, instruction);
                prev = instruction;
            }
        }

        public static void InsertBefore(this ILProcessor ilProcessor, Instruction insertBeforeThisOne,
            IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions)
                ilProcessor.InsertBefore(insertBeforeThisOne, instruction);
        }
    }
}
