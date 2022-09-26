﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using ILLink.Shared;
using ILLink.Shared.DataFlow;
using ILLink.Shared.TrimAnalysis;
using ILLink.Shared.TypeSystemProxy;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using static Mono.Linker.ParameterHelpers;
using MultiValue = ILLink.Shared.DataFlow.ValueSet<ILLink.Shared.DataFlow.SingleValue>;

namespace Mono.Linker.Dataflow
{
	abstract class Scanner
		: ITransfer<
			BasicBlock,
			BasicBlockState<MultiValue>,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>>,
			BlockStateLattice<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>>
		>
	{
		protected readonly ReflectionHandler _handler;
		private readonly LinkContext _context;
		private static ValueSetLatticeWithUnknownValue<SingleValue> MultiValueLattice => default;

		public MultiValue ReturnValue { private set; get; }


		public Scanner (LinkContext context, ReflectionHandler handler)
		{
			_context = context;
			_handler = handler;
		}

		public virtual void Scan (BasicBlock block, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			MethodDefinition thisMethod = block.MethodBody.Method;


			ReturnValue = new ();
			foreach (Instruction operation in block.GetInstructions ()) {
				switch (operation.OpCode.Code) {
				case Code.Add:
				case Code.Add_Ovf:
				case Code.Add_Ovf_Un:
				case Code.And:
				case Code.Div:
				case Code.Div_Un:
				case Code.Mul:
				case Code.Mul_Ovf:
				case Code.Mul_Ovf_Un:
				case Code.Or:
				case Code.Rem:
				case Code.Rem_Un:
				case Code.Sub:
				case Code.Sub_Ovf:
				case Code.Sub_Ovf_Un:
				case Code.Xor:
				case Code.Cgt:
				case Code.Cgt_Un:
				case Code.Clt:
				case Code.Clt_Un:
				case Code.Shl:
				case Code.Shr:
				case Code.Shr_Un:
				case Code.Ceq:
					PopUnknown (state, 2, thisMethod.Body, operation.Offset);
					PushUnknown (state);
					break;

				case Code.Dup:
					var topVal = state.Pop ();
					state.Push (topVal);
					state.Push (topVal);
					break;

				case Code.Ldnull:
					state.Push (new MultiValue (NullValue.Instance));
					break;

				case Code.Ldc_I4_0:
				case Code.Ldc_I4_1:
				case Code.Ldc_I4_2:
				case Code.Ldc_I4_3:
				case Code.Ldc_I4_4:
				case Code.Ldc_I4_5:
				case Code.Ldc_I4_6:
				case Code.Ldc_I4_7:
				case Code.Ldc_I4_8: {
						int value = operation.OpCode.Code - Code.Ldc_I4_0;
						ConstIntValue civ = new ConstIntValue (value);
						MultiValue mv = new MultiValue (civ);
						state.Push (mv);
					}
					break;

				case Code.Ldc_I4_M1: {
						ConstIntValue civ = new ConstIntValue (-1);
						MultiValue mv = new MultiValue (civ);
						state.Push (mv);
					}
					break;

				case Code.Ldc_I4: {
						int value = (int) operation.Operand;
						ConstIntValue civ = new ConstIntValue (value);
						MultiValue mv = new MultiValue (civ);
						state.Push (mv);
					}
					break;

				case Code.Ldc_I4_S: {
						int value = (sbyte) operation.Operand;
						ConstIntValue civ = new ConstIntValue (value);
						MultiValue mv = new MultiValue (civ);
						state.Push (mv);
					}
					break;

				case Code.Arglist:
				case Code.Sizeof:
				case Code.Ldc_I8:
				case Code.Ldc_R4:
				case Code.Ldc_R8:
					PushUnknown (state);
					break;

				case Code.Ldftn:
					//TrackNestedFunctionReference ((MethodReference) operation.Operand, ref interproceduralState);
					PushUnknown (state);
					break;

				case Code.Ldarg:
				case Code.Ldarg_0:
				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
				case Code.Ldarg_S:
				case Code.Ldarga:
				case Code.Ldarga_S:
					ScanLdarg (operation, state, thisMethod);
					break;

				case Code.Ldloc:
				case Code.Ldloc_0:
				case Code.Ldloc_1:
				case Code.Ldloc_2:
				case Code.Ldloc_3:
				case Code.Ldloc_S:
				case Code.Ldloca:
				case Code.Ldloca_S:
					ScanLdloc (operation, state, thisMethod);
					//ValidateNoReferenceToReference (locals, methodBody.Method, operation.Offset);
					break;

				case Code.Ldstr: {
						MultiValue mv = new MultiValue (new KnownStringValue ((string) operation.Operand));
						state.Push (mv);
					}
					break;

				case Code.Ldtoken:
					ScanLdtoken (operation, state);
					break;

				case Code.Ldind_I:
				case Code.Ldind_I1:
				case Code.Ldind_I2:
				case Code.Ldind_I4:
				case Code.Ldind_I8:
				case Code.Ldind_R4:
				case Code.Ldind_R8:
				case Code.Ldind_U1:
				case Code.Ldind_U2:
				case Code.Ldind_U4:
				case Code.Ldlen:
				case Code.Ldvirtftn:
				case Code.Localloc:
				case Code.Refanytype:
				case Code.Refanyval:
				case Code.Conv_I1:
				case Code.Conv_I2:
				case Code.Conv_I4:
				case Code.Conv_Ovf_I1:
				case Code.Conv_Ovf_I1_Un:
				case Code.Conv_Ovf_I2:
				case Code.Conv_Ovf_I2_Un:
				case Code.Conv_Ovf_I4:
				case Code.Conv_Ovf_I4_Un:
				case Code.Conv_Ovf_U:
				case Code.Conv_Ovf_U_Un:
				case Code.Conv_Ovf_U1:
				case Code.Conv_Ovf_U1_Un:
				case Code.Conv_Ovf_U2:
				case Code.Conv_Ovf_U2_Un:
				case Code.Conv_Ovf_U4:
				case Code.Conv_Ovf_U4_Un:
				case Code.Conv_U1:
				case Code.Conv_U2:
				case Code.Conv_U4:
				case Code.Conv_I8:
				case Code.Conv_Ovf_I8:
				case Code.Conv_Ovf_I8_Un:
				case Code.Conv_Ovf_U8:
				case Code.Conv_Ovf_U8_Un:
				case Code.Conv_U8:
				case Code.Conv_I:
				case Code.Conv_Ovf_I:
				case Code.Conv_Ovf_I_Un:
				case Code.Conv_U:
				case Code.Conv_R_Un:
				case Code.Conv_R4:
				case Code.Conv_R8:
				case Code.Ldind_Ref:
				case Code.Ldobj:
				case Code.Mkrefany:
				case Code.Unbox:
				case Code.Unbox_Any:
				case Code.Box:
				case Code.Neg:
				case Code.Not:
					PopUnknown (state, 1, thisMethod.Body, operation.Offset);
					PushUnknown (state);
					break;

				case Code.Isinst:
				case Code.Castclass:
					// We can consider a NOP because the value doesn't change.
					// It might change to NULL, but for the purposes of dataflow analysis
					// it doesn't hurt much.
					break;

				case Code.Ldfld:
				case Code.Ldsfld:
				case Code.Ldflda:
				case Code.Ldsflda:
					ScanLdfld (operation, state, thisMethod.Body);
					break;

				case Code.Newarr: {
						MultiValue count = PopUnknown (state, 1, thisMethod.Body, operation.Offset);
						state.Push (new MultiValue (ArrayValue.Create (count, (TypeReference) operation.Operand)));
					}
					break;

				case Code.Stelem_I:
				case Code.Stelem_I1:
				case Code.Stelem_I2:
				case Code.Stelem_I4:
				case Code.Stelem_I8:
				case Code.Stelem_R4:
				case Code.Stelem_R8:
				case Code.Stelem_Any:
				case Code.Stelem_Ref:
					ScanStelem (operation, block, state, thisMethod.Body);
					break;

				case Code.Ldelem_I:
				case Code.Ldelem_I1:
				case Code.Ldelem_I2:
				case Code.Ldelem_I4:
				case Code.Ldelem_I8:
				case Code.Ldelem_R4:
				case Code.Ldelem_R8:
				case Code.Ldelem_U1:
				case Code.Ldelem_U2:
				case Code.Ldelem_U4:
				case Code.Ldelem_Any:
				case Code.Ldelem_Ref:
				case Code.Ldelema:
					ScanLdelem (operation, block, state, thisMethod.Body);
					break;

				case Code.Cpblk:
				case Code.Initblk:
					PopUnknown (state, 3, thisMethod.Body, operation.Offset);
					break;

				case Code.Stfld:
				case Code.Stsfld:
					ScanStfld (operation, state, thisMethod);
					break;

				case Code.Cpobj:
					PopUnknown (state, 2, thisMethod.Body, operation.Offset);
					break;

				case Code.Stind_I:
				case Code.Stind_I1:
				case Code.Stind_I2:
				case Code.Stind_I4:
				case Code.Stind_I8:
				case Code.Stind_R4:
				case Code.Stind_R8:
				case Code.Stind_Ref:
				case Code.Stobj:
					ScanIndirectStore (operation, state, thisMethod.Body);
					//ValidateNoReferenceToReference (locals, methodBody.Method, operation.Offset);
					break;

				case Code.Initobj:
				case Code.Pop:
					PopUnknown (state, 1, thisMethod.Body, operation.Offset);
					break;

				case Code.Starg:
				case Code.Starg_S:
					ScanStarg (operation, state, thisMethod);
					break;

				case Code.Stloc:
				case Code.Stloc_S:
				case Code.Stloc_0:
				case Code.Stloc_1:
				case Code.Stloc_2:
				case Code.Stloc_3:
					ScanStloc (operation, state, thisMethod.Body);
					//ValidateNoReferenceToReference (locals, methodBody.Method, operation.Offset);
					break;

				case Code.Constrained:
				case Code.No:
				case Code.Readonly:
				case Code.Tail:
				case Code.Unaligned:
				case Code.Volatile:
					break;

				case Code.Brfalse:
				case Code.Brfalse_S:
				case Code.Brtrue:
				case Code.Brtrue_S:
					PopUnknown (state, 1, thisMethod.Body, operation.Offset);
					break;

				case Code.Calli: {
						var signature = (CallSite) operation.Operand;
						if (signature.HasThis && !signature.ExplicitThis) {
							PopUnknown (state, 1, thisMethod.Body, operation.Offset);
						}

						// Pop arguments
						if (signature.Parameters.Count > 0)
							PopUnknown (state, 1, thisMethod.Body, operation.Offset);

						// Pop function pointer
						PopUnknown (state, 1, thisMethod.Body, operation.Offset);

						if (!signature.ReturnsVoid ())
							PushUnknown (state);
					}
					break;

				case Code.Call:
				case Code.Callvirt:
				case Code.Newobj:
					//TrackNestedFunctionReference ((MethodReference) operation.Operand, ref interproceduralState);
					HandleCall (thisMethod.Body, operation, state);
					//ValidateNoReferenceToReference (locals, methodBody.Method, operation.Offset);
					break;

				case Code.Jmp:
					// Not generated by mainstream compilers
					break;

				case Code.Br:
				case Code.Br_S:
					break;

				case Code.Leave:
				case Code.Leave_S:
					break;

				case Code.Endfilter:
				case Code.Endfinally:
				case Code.Rethrow:
				case Code.Throw:
					break;

				case Code.Ret: {

						bool hasReturnValue = !thisMethod.ReturnsVoid ();

						if (state.Current.Stack.Count != (hasReturnValue ? 1 : 0)) {
							ReflectionHandler.WarnAboutInvalidILInMethod (thisMethod.Body, operation.Offset);
						}
						if (hasReturnValue) {
							MultiValue retValue = PopUnknown (state, 1, thisMethod.Body, operation.Offset);
							// If the return value is a reference, treat it as the value itself for now
							//	We can handle ref return values better later
							ReturnValue = MultiValueLattice.Meet (ReturnValue, DereferenceValue (retValue, state));
							//ValidateNoReferenceToReference (locals, methodBody.Method, operation.Offset);
						}
						_handler.HandleReturnValue (thisMethod, ReturnValue); // Should be done once, after end of the analysis
						break;
					}

				case Code.Switch: {
						PopUnknown (state, 1, thisMethod.Body, operation.Offset);
						break;
					}

				case Code.Beq:
				case Code.Beq_S:
				case Code.Bne_Un:
				case Code.Bne_Un_S:
				case Code.Bge:
				case Code.Bge_S:
				case Code.Bge_Un:
				case Code.Bge_Un_S:
				case Code.Bgt:
				case Code.Bgt_S:
				case Code.Bgt_Un:
				case Code.Bgt_Un_S:
				case Code.Ble:
				case Code.Ble_S:
				case Code.Ble_Un:
				case Code.Ble_Un_S:
				case Code.Blt:
				case Code.Blt_S:
				case Code.Blt_Un:
				case Code.Blt_Un_S:
					PopUnknown (state, 2, thisMethod.Body, operation.Offset);
					break;
				}
			}
		}

		public void Transfer (BasicBlock block, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			Scan (block, state);
		}

		private void ScanLdarg (Instruction operation, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state, MethodDefinition thisMethod)
		{
			Code code = operation.OpCode.Code;
			bool isByRef = code == Code.Ldarga || code == Code.Ldarga_S;

			SingleValue valueToPush;
			switch (GetSourceParameterIndex (thisMethod, operation, out var sourceIndex)) {
			case SourceParameterKind.Numbered:
				// This is semantically wrong if it returns true - we would representing a reference parameter as a reference to a parameter - but it should be fine for now
				isByRef |= thisMethod.GetParameterType (sourceIndex).IsByRefOrPointer ();
				valueToPush = isByRef
					? new ParameterReferenceValue (thisMethod, sourceIndex)
					: _handler.GetMethodParameterValue (thisMethod, sourceIndex);
				break;
			case SourceParameterKind.This:
				isByRef |= thisMethod.DeclaringType.IsValueType;
				valueToPush = isByRef
					? new ThisParameterReferenceValue (thisMethod)
					: _handler.GetMethodThisParameterValue (thisMethod);
				break;
			default:
				throw new InvalidOperationException ("Unexpected IParameterIndex type");
			}

			MultiValue mv = new MultiValue (valueToPush);
			state.Push (mv);
		}

		private void ScanStarg (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodDefinition thisMethod)
		{
			var valueToStore = PopUnknown (state, 1, thisMethod.Body, operation.Offset);
			switch (GetSourceParameterIndex (thisMethod, operation, out var sourceParameterIndex)) {
			case SourceParameterKind.Numbered:
				var targetValue = _handler.GetMethodParameterValue (thisMethod, sourceParameterIndex);
				if (targetValue is MethodParameterValue targetParameterValue)
					_handler.HandleStoreParameter (targetParameterValue, operation, valueToStore);
				break;
			// If the targetValue is MethodThisValue do nothing - it should never happen really, and if it does, there's nothing we can track there
			case SourceParameterKind.This:
				break;
			default:
				break;
			}
		}

		private static void ScanLdloc (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodDefinition method)
		{
			VariableDefinition localDef = GetLocalDef (operation, method.Body.Variables);
			if (localDef == null) {
				PushUnknownAndWarnAboutInvalidIL (state, method.Body, operation.Offset);
				return;
			}

			bool isByRef = operation.OpCode.Code == Code.Ldloca || operation.OpCode.Code == Code.Ldloca_S;

			MultiValue newSlot;
			if (isByRef) {
				newSlot = new MultiValue (new LocalVariableReferenceValue (localDef));
			} else
				newSlot = new MultiValue (state.GetLocal (new LocalKey (localDef)));
			state.Push (newSlot);
		}

		void ScanLdtoken (Instruction operation, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			switch (operation.Operand) {
			case GenericParameter genericParameter:
				var param = new RuntimeTypeHandleForGenericParameterValue (genericParameter);
				state.Push (new MultiValue (param));
				return;
			case TypeReference typeReference when ResolveToTypeDefinition (typeReference) is TypeDefinition resolvedDefinition:
				// Note that Nullable types without a generic argument (i.e. Nullable<>) will be RuntimeTypeHandleValue / SystemTypeValue
				if (typeReference is IGenericInstance instance && resolvedDefinition.IsTypeOf (WellKnownType.System_Nullable_T)) {
					switch (instance.GenericArguments[0]) {
					case GenericParameter genericParam:
						var nullableDam = new RuntimeTypeHandleForNullableValueWithDynamicallyAccessedMembers (new TypeProxy (resolvedDefinition),
							new RuntimeTypeHandleForGenericParameterValue (genericParam));
						state.Push (new MultiValue (nullableDam));
						return;
					case TypeReference underlyingTypeReference when ResolveToTypeDefinition (underlyingTypeReference) is TypeDefinition underlyingType:
						var nullableType = new RuntimeTypeHandleForNullableSystemTypeValue (new TypeProxy (resolvedDefinition), new SystemTypeValue (underlyingType));
						state.Push (new MultiValue (nullableType));
						return;
					default:
						PushUnknown (state);
						return;
					}
				} else {
					var typeHandle = new RuntimeTypeHandleValue (new TypeProxy (resolvedDefinition));
					state.Push (new MultiValue (typeHandle));
					return;
				}
			case MethodReference methodReference when _context.TryResolve (methodReference) is MethodDefinition resolvedMethod:
				var method = new RuntimeMethodHandleValue (resolvedMethod);
				state.Push (new MultiValue (method));
				return;
			default:
				PushUnknown (state);
				return;
			}
		}

		private static void ScanStloc (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodBody methodBody)
		{
			MultiValue valueToStore = PopUnknown (state, 1, methodBody, operation.Offset);
			VariableDefinition localDef = GetLocalDef (operation, methodBody.Variables);
			if (localDef == null) {
				ReflectionHandler.WarnAboutInvalidILInMethod (methodBody, operation.Offset);
				return;
			}
			state.SetLocal (new LocalKey (localDef), valueToStore);
		}

		private void ScanIndirectStore (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodBody methodBody)
		{
			MultiValue valueToStore = PopUnknown (state, 1, methodBody, operation.Offset);
			MultiValue destination = PopUnknown (state, 1, methodBody, operation.Offset);

			StoreInReference (destination, valueToStore, methodBody.Method, operation, state);
		}

		/// <summary>
		/// Handles storing the source value in a target <see cref="ReferenceValue"/> or MultiValue of ReferenceValues.
		/// </summary>
		/// <param name="target">A set of <see cref="ReferenceValue"/> that a value is being stored into</param>
		/// <param name="source">The value to store</param>
		/// <param name="method">The method body that contains the operation causing the store</param>
		/// <param name="operation">The instruction causing the store</param>
		/// <exception cref="LinkerFatalErrorException">Throws if <paramref name="target"/> is not a valid target for an indirect store.</exception>
		protected void StoreInReference (MultiValue target, MultiValue source, MethodDefinition method, Instruction operation, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			foreach (var value in target) {
				switch (value) {
				case LocalVariableReferenceValue localReference:
					state.SetLocal (new LocalKey (localReference.LocalDefinition), source);
					break;
				case FieldReferenceValue fieldReference
				when _handler.GetFieldValue (fieldReference.FieldDefinition).AsSingleValue () is FieldValue fieldValue:
					_handler.HandleStoreField (fieldValue, operation, source);
					break;
				case ParameterReferenceValue parameterReference
				when _handler.GetMethodParameterValue (parameterReference.MethodDefinition, parameterReference.ParameterIndex) is MethodParameterValue parameterValue:
					_handler.HandleStoreParameter (parameterValue, operation, source);
					break;
				case ThisParameterReferenceValue parameterReference
					when _handler.GetMethodThisParameterValue (parameterReference.MethodDefinition) is MethodThisParameterValue thisParameterValue:
					break;
				case MethodReturnValue methodReturnValue:
					// Ref returns don't have special ReferenceValue values, so assume if the target here is a MethodReturnValue then it must be a ref return value
					_handler.HandleStoreMethodReturnValue (methodReturnValue, operation, source);
					break;
				case FieldValue fieldValue:
					_handler.HandleStoreField (fieldValue, operation, DereferenceValue (source, state));
					break;
				case IValueWithStaticType valueWithStaticType:
					if (valueWithStaticType.StaticType is not null && _context.Annotations.FlowAnnotations.IsTypeInterestingForDataflow (valueWithStaticType.StaticType))
						throw new LinkerFatalErrorException (MessageContainer.CreateErrorMessage (
							$"Unhandled StoreReference call. Unhandled attempt to store a value in {value} of type {value.GetType ()}.",
							(int) DiagnosticId.LinkerUnexpectedError,
							origin: new MessageOrigin (method, operation.Offset)));
					// This should only happen for pointer derefs, which can't point to interesting types
					break;
				default:
					// These cases should only be refs to array elements
					// References to array elements are not yet tracked and since we don't allow annotations on arrays, they won't cause problems
					break;
				}
			}
		}

		private void ScanLdfld (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodBody methodBody)
		{
			Code code = operation.OpCode.Code;
			if (code == Code.Ldfld || code == Code.Ldflda)
				PopUnknown (state, 1, methodBody, operation.Offset);

			bool isByRef = code == Code.Ldflda || code == Code.Ldsflda;

			FieldDefinition? field = _context.TryResolve ((FieldReference) operation.Operand);
			if (field == null) {
				PushUnknown (state);
				return;
			}

			MultiValue value;
			if (isByRef) {
				value = new FieldReferenceValue (field);
				//} else if (CompilerGeneratedState.IsHoistedLocal (field)) {
				//	value = interproceduralState.GetHoistedLocal (new HoistedLocalKey (field));
			} else {
				value = _handler.GetFieldValue (field);
			}
			state.Push (new MultiValue (value));
		}

		private void ScanStfld (
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodDefinition method)
		{
			MultiValue valueToStoreSlot = PopUnknown (state, 1, method.Body, operation.Offset);
			if (operation.OpCode.Code == Code.Stfld)
				PopUnknown (state, 1, method.Body, operation.Offset);

			FieldDefinition? field = _context.TryResolve ((FieldReference) operation.Operand);
			if (field != null) {
				if (CompilerGeneratedState.IsHoistedLocal (field)) {
					//	interproceduralState.SetHoistedLocal (new HoistedLocalKey (field), valueToStoreSlot);
					return;
				}

				foreach (var value in _handler.GetFieldValue (field)) {
					// GetFieldValue may return different node types, in which case they can't be stored to.
					// At least not yet.
					if (value is not FieldValue fieldValue)
						continue;

					// Incomplete handling of ref fields -- if we're storing a reference to a value, pretend it's just the value
					MultiValue valueToStore = DereferenceValue (valueToStoreSlot, state);

					_handler.HandleStoreField (fieldValue, operation, valueToStore);
				}
			}
		}

		private static VariableDefinition GetLocalDef (Instruction operation, Collection<VariableDefinition> localVariables)
		{
			Code code = operation.OpCode.Code;
			if (code >= Code.Ldloc_0 && code <= Code.Ldloc_3)
				return localVariables[code - Code.Ldloc_0];
			if (code >= Code.Stloc_0 && code <= Code.Stloc_3)
				return localVariables[code - Code.Stloc_0];

			return (VariableDefinition) operation.Operand;
		}

		private static ValueNodeList PopCallArguments (
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodReference methodCalled,
			MethodBody containingMethodBody,
			bool isNewObj, int ilOffset,
			out SingleValue? newObjValue)
		{
			newObjValue = null;

			int countToPop = 0;
			if (!isNewObj && methodCalled.HasThis && !methodCalled.ExplicitThis)
				countToPop++;
			countToPop += methodCalled.Parameters.Count;

			ValueNodeList methodParams = new ValueNodeList (countToPop);
			for (int iParam = 0; iParam < countToPop; ++iParam) {
				MultiValue slot = PopUnknown (state, 1, containingMethodBody, ilOffset);
				methodParams.Add (slot);
			}

			if (isNewObj) {
				newObjValue = UnknownValue.Instance;
				methodParams.Add (newObjValue);
			}
			methodParams.Reverse ();
			return methodParams;
		}

		internal MultiValue DereferenceValue (MultiValue maybeReferenceValue, BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			MultiValue dereferencedValue = MultiValueLattice.Top;
			foreach (var value in maybeReferenceValue) {
				switch (value) {
				case FieldReferenceValue fieldReferenceValue:
					dereferencedValue = MultiValue.Meet (
						dereferencedValue,
						_handler.GetFieldValue (fieldReferenceValue.FieldDefinition));
					break;
				case ParameterReferenceValue parameterReferenceValue:
					dereferencedValue = MultiValue.Meet (
						dereferencedValue,
						_handler.GetMethodParameterValue (parameterReferenceValue.MethodDefinition, parameterReferenceValue.ParameterIndex));
					break;
				case LocalVariableReferenceValue localVariableReferenceValue:
					dereferencedValue = MultiValue.Meet (dereferencedValue, state.GetLocal (new LocalKey (localVariableReferenceValue.LocalDefinition)));
					break;
				case ThisParameterReferenceValue thisParameterReferenceValue:
					dereferencedValue = MultiValue.Meet (
						dereferencedValue,
						_handler.GetMethodThisParameterValue (thisParameterReferenceValue.MethodDefinition));
					break;
				case ReferenceValue referenceValue:
					throw new NotImplementedException ($"Unhandled dereference of ReferenceValue of type {referenceValue.GetType ().FullName}");
				// Incomplete handling for ref values
				case FieldValue fieldValue:
					dereferencedValue = MultiValue.Meet (dereferencedValue, fieldValue);
					break;
				default:
					dereferencedValue = MultiValue.Meet (dereferencedValue, value);
					break;
				}
			}
			return dereferencedValue;
		}

		/// <summary>
		/// Assigns a MethodParameterValue to the location of each parameter passed by reference. (i.e. assigns the value to x when passing `ref x` as a parameter)
		/// </summary>
		protected void AssignRefAndOutParameters (
			MethodBody callingMethodBody,
			MethodReference calledMethod,
			ValueNodeList methodArguments,
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			MethodDefinition? calledMethodDefinition = _context.Resolve (calledMethod);
			bool methodIsResolved = calledMethodDefinition is not null;
			ILParameterIndex ilArgumentIndex;
			for (SourceParameterIndex parameterIndex = 0; (int) parameterIndex < calledMethod.Parameters.Count; parameterIndex++) {
				ilArgumentIndex = GetILParameterIndex (calledMethod, parameterIndex);

				if (calledMethod.ParameterReferenceKind ((int) ilArgumentIndex) is not (ReferenceKind.Ref or ReferenceKind.Out))
					continue;
				SingleValue newByRefValue = methodIsResolved && (int) parameterIndex < calledMethodDefinition!.Parameters.Count
					? _context.Annotations.FlowAnnotations.GetMethodParameterValue (calledMethodDefinition!, parameterIndex)
					: UnknownValue.Instance;
				StoreInReference (methodArguments[(int) ilArgumentIndex], newByRefValue, callingMethodBody.Method, operation, state);
			}
		}

		private void HandleCall (
			MethodBody callingMethodBody,
			Instruction operation,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			MethodReference calledMethod = (MethodReference) operation.Operand;

			bool isNewObj = operation.OpCode.Code == Code.Newobj;

			SingleValue? newObjValue;
			ValueNodeList methodArguments = PopCallArguments (state, calledMethod, callingMethodBody, isNewObj, operation.Offset, out newObjValue);

			var dereferencedMethodParams = new List<MultiValue> ();
			foreach (var argument in methodArguments)
				dereferencedMethodParams.Add (DereferenceValue (argument, state));
			MultiValue methodReturnValue;
			bool handledFunction = _handler.HandleCall (
				callingMethodBody,
				calledMethod,
				operation,
				new ValueNodeList (dereferencedMethodParams),
				out methodReturnValue);

			// Handle the return value or newobj result
			if (!handledFunction) {
				if (isNewObj) {
					if (newObjValue == null)
						methodReturnValue = new MultiValue (UnknownValue.Instance);
					else
						methodReturnValue = newObjValue;
				} else {
					if (!calledMethod.ReturnsVoid ()) {
						methodReturnValue = UnknownValue.Instance;
					}
				}
			}

			if (isNewObj || !calledMethod.ReturnsVoid ())
				state.Push (new MultiValue (methodReturnValue));

			AssignRefAndOutParameters (callingMethodBody, calledMethod, methodArguments, operation, state);

			foreach (var param in methodArguments) {
				foreach (var v in param) {
					if (v is ArrayValue arr) {
						MarkArrayValuesAsUnknown (arr);
					}
				}
			}
		}

		public TypeDefinition? ResolveToTypeDefinition (TypeReference typeReference) => typeReference.ResolveToTypeDefinition (_context);

		// Limit tracking array values to 32 values for performance reasons. There are many arrays much longer than 32 elements in .NET, but the interesting ones for the linker are nearly always less than 32 elements.
		private const int MaxTrackedArrayValues = 32;

		private static void MarkArrayValuesAsUnknown (ArrayValue arrValue)
		{
			// Since we can't know the current index we're storing the value at, clear all indices.
			// That way we won't accidentally think we know the value at a given index when we cannot.
			arrValue.IndexValues.Clear ();
		}

		private static void ScanStelem (
			Instruction operation,
			BasicBlock block,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodBody methodBody)
		{
			MultiValue valueToStore = PopUnknown (state, 1, methodBody, operation.Offset);
			MultiValue indexToStoreAt = PopUnknown (state, 1, methodBody, operation.Offset);
			MultiValue arrayToStoreIn = PopUnknown (state, 1, methodBody, operation.Offset);
			int? indexToStoreAtInt = indexToStoreAt.AsConstInt ();
			foreach (var array in arrayToStoreIn) {
				if (array is ArrayValue arrValue) {
					if (indexToStoreAtInt == null) {
						MarkArrayValuesAsUnknown (arrValue);
					} else {
						// When we know the index, we can record the value at that index.
						StoreMethodLocalValue (arrValue.IndexValues, valueToStore, indexToStoreAtInt.Value, block.Id, MaxTrackedArrayValues);
					}
				}
			}
		}

		private static void ScanLdelem (
			Instruction operation,
			BasicBlock block,
			BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state,
			MethodBody methodBody)
		{
			MultiValue indexToLoadFrom = PopUnknown (state, 1, methodBody, operation.Offset);
			MultiValue arrayToLoadFrom = PopUnknown (state, 1, methodBody, operation.Offset);

			bool isByRef = operation.OpCode.Code == Code.Ldelema;

			int? index = indexToLoadFrom.AsConstInt ();
			if (index == null) {
				PushUnknown (state);
				foreach (var value in arrayToLoadFrom) {
					if (isByRef && value is ArrayValue arr) {
						MarkArrayValuesAsUnknown (arr);
					}
				}
				return;
			}

			if (isByRef) {
				foreach (var value in arrayToLoadFrom) {
					if (value is ArrayValue arr) {
						arr.IndexValues[index.Value] = new ValueBasicBlockPair (UnknownValue.Instance, block.Id);
					}
				}
				PushUnknown (state);
				return;
			}

			MultiValue result = MultiValueLattice.Top;

			foreach (var value in arrayToLoadFrom) {
				if (value is ArrayValue arr && arr.TryGetValueByIndex (index.Value, out var elementValue))
					result = MultiValueLattice.Meet (result, elementValue);
				else
					result = MultiValueLattice.Meet (result, UnknownValue.Instance);
			}

			if (result.Equals (MultiValueLattice.Top))
				PushUnknown (state);
			else
				state.Push (result);
		}

		protected static void StoreMethodLocalValue<KeyType> (
			Dictionary<KeyType, ValueBasicBlockPair> valueCollection,
			in MultiValue valueToStore,
			KeyType collectionKey,
			int curBasicBlock,
			int? maxTrackedValues = null)
			where KeyType : notnull
		{
			if (valueCollection.TryGetValue (collectionKey, out ValueBasicBlockPair existingValue)) {
				MultiValue value;
				if (existingValue.BasicBlockIndex == curBasicBlock) {
					// If the previous value was stored in the current basic block, then we can safely
					// overwrite the previous value with the new one.
					value = valueToStore;
				} else {
					// If the previous value came from a previous basic block, then some other use of
					// the local could see the previous value, so we must merge the new value with the
					// old value.
					value = MultiValueLattice.Meet (existingValue.Value, valueToStore);
				}
				valueCollection[collectionKey] = new ValueBasicBlockPair (value, curBasicBlock);
			} else if (maxTrackedValues == null || valueCollection.Count < maxTrackedValues) {
				// We're not currently tracking a value a this index, so store the value now.
				valueCollection[collectionKey] = new ValueBasicBlockPair (valueToStore, curBasicBlock);
			}
		}

		private static void CheckForInvalidStack (BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state, int depthRequired, MethodBody method, int ilOffset)
		{
			if (state.Current.Stack.Count < depthRequired) {
				ReflectionHandler.WarnAboutInvalidILInMethod (method, ilOffset);
				while (state.Current.Stack.Count < depthRequired)
					state.Push (new MultiValue (UnknownValue.Instance)); // Push dummy values to avoid crashes.
																		 // Analysis of this method will be incorrect.
			}
		}

		private static MultiValue PopUnknown (BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state, int count, MethodBody method, int ilOffset)
		{
			if (count < 1)
				throw new InvalidOperationException ();

			CheckForInvalidStack (state, count, method, ilOffset);

			return state.Pop (count);
		}

		private static void PushUnknown (BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state)
		{
			state.Push (new MultiValue (UnknownValue.Instance));
		}

		private static void PushUnknownAndWarnAboutInvalidIL (BasicBlockDataFlowState<MultiValue, ValueSetLatticeWithUnknownValue<SingleValue>> state, MethodBody methodBody, int offset)
		{
			ReflectionHandler.WarnAboutInvalidILInMethod (methodBody, offset);
			PushUnknown (state);
		}
	}
}
