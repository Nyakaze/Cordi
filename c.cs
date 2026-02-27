using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Mathematics;

namespace Vortice.Direct3D11;

/// <summary>
///       <para>The ID3D11DeviceContext interface represents a device context which generates rendering commands.</para>
///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nn-d3d11-id3d11devicecontext" /></para>
///     </summary>
/// <unmanaged>ID3D11DeviceContext</unmanaged>
/// <unmanaged-short>ID3D11DeviceContext</unmanaged-short>
[Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
public class ID3D11DeviceContext : ID3D11DeviceChild
{
	/// <summary>
	/// D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL
	/// </summary>
	public const int KeepRenderTargetsAndDepthStencil = -1;

	/// <summary>
	/// D3D11_KEEP_UNORDERED_ACCESS_VIEWS
	/// </summary>
	public const int KeepUnorderedAccessViews = -1;

	public const uint DefaultSampleMask = uint.MaxValue;

	private unsafe static readonly void*[] s_NullBuffers = new void*[14]
	{
		null, null, null, null, null, null, null, null, null, null,
		null, null, null, null
	};

	private unsafe static readonly void*[] s_NullSamplers = new void*[16]
	{
		null, null, null, null, null, null, null, null, null, null,
		null, null, null, null, null, null
	};

	private unsafe static readonly void*[] s_NullUAVs = new void*[8] { null, null, null, null, null, null, null, null };

	private static readonly int[] s_NegativeOnes = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };

	private bool? _supportsCommandLists;

	/// <summary>
	/// Constant UnorderedAccessViewSlotCount
	/// </summary>
	/// <unmanaged>D3D11_1_UAV_SLOT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_1_UAV_SLOT_COUNT</unmanaged-short>
	public const int UnorderedAccessViewSlotCount = 64;

	/// <summary>
	/// Constant UnorderedAccessViewRegisterCount
	/// </summary>
	/// <unmanaged>D3D11_PS_CS_UAV_REGISTER_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_PS_CS_UAV_REGISTER_COUNT</unmanaged-short>
	public const int UnorderedAccessViewRegisterCount = 8;

	/// <summary>
	/// Constant CommonShaderConstantBufferSlotCount
	/// </summary>
	/// <unmanaged>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</unmanaged-short>
	public const int CommonShaderConstantBufferSlotCount = 14;

	/// <summary>
	/// Constant CommonShaderSamplerSlotCount
	/// </summary>
	/// <unmanaged>D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT</unmanaged-short>
	public const int CommonShaderSamplerSlotCount = 16;

	/// <summary>
	/// Constant CommonShaderInputResourceSlotCount
	/// </summary>
	/// <unmanaged>D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT</unmanaged-short>
	public const int CommonShaderInputResourceSlotCount = 128;

	/// <summary>
	/// Constant ViewportAndScissorRectObjectCountPerPipeline
	/// </summary>
	/// <unmanaged>D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE</unmanaged>
	/// <unmanaged-short>D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE</unmanaged-short>
	public const int ViewportAndScissorRectObjectCountPerPipeline = 16;

	/// <summary>
	/// Constant InputAssemblerVertexInputResourceSlotCount
	/// </summary>
	/// <unmanaged>D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT</unmanaged-short>
	public const int InputAssemblerVertexInputResourceSlotCount = 32;

	/// <summary>
	/// Constant ConstantBufferElementCount
	/// </summary>
	/// <unmanaged>D3D11_REQ_CONSTANT_BUFFER_ELEMENT_COUNT</unmanaged>
	/// <unmanaged-short>D3D11_REQ_CONSTANT_BUFFER_ELEMENT_COUNT</unmanaged-short>
	public const int ConstantBufferElementCount = 4096;

	/// <summary>
	/// Constant ViewportBoundsMax
	/// </summary>
	/// <unmanaged>D3D11_VIEWPORT_BOUNDS_MAX</unmanaged>
	/// <unmanaged-short>D3D11_VIEWPORT_BOUNDS_MAX</unmanaged-short>
	public const int ViewportBoundsMax = 32767;

	/// <summary>
	/// Constant ViewportBoundsMin
	/// </summary>
	/// <unmanaged>D3D11_VIEWPORT_BOUNDS_MIN</unmanaged>
	/// <unmanaged-short>D3D11_VIEWPORT_BOUNDS_MIN</unmanaged-short>
	public const int ViewportBoundsMin = -32768;

	private uint OMSetBlendState__vtbl_index = 35u;

	private uint OMGetBlendState__vtbl_index = 91u;

	/// <summary>
	///       <para>Gets the type of device context.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gettype" /></para>
	///     </summary>
	/// <unmanaged>D3D11_DEVICE_CONTEXT_TYPE ID3D11DeviceContext::GetType()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetType</unmanaged-short>
	public DeviceContextType ContextType => GetContextType();

	/// <summary>
	///       <para>Gets the initialization flags associated with the current deferred context.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-getcontextflags" /></para>
	///     </summary>
	/// <unmanaged>UINT ID3D11DeviceContext::GetContextFlags()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetContextFlags</unmanaged-short>
	public int ContextFlags => GetContextFlags();

	public void ClearRenderTargetView(ID3D11RenderTargetView renderTargetView, in Color4 color)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Vector4 vector = Color4.op_Implicit(color);
		this.ClearRenderTargetView(renderTargetView, new Color4(ref vector));
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState)
	{
		IntPtr intPtr = ((blendState != null) ? ((CppObject)blendState).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, float*, uint, void>)((CppObject)this)[OMSetBlendState__vtbl_index])(((CppObject)this).NativePointer, (void*)intPtr, null, uint.MaxValue);
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState, float* blendFactor)
	{
		IntPtr intPtr = ((blendState != null) ? ((CppObject)blendState).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, float*, uint, void>)((CppObject)this)[OMSetBlendState__vtbl_index])(((CppObject)this).NativePointer, (void*)intPtr, blendFactor, uint.MaxValue);
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState, float* blendFactor, uint sampleMask)
	{
		IntPtr intPtr = ((blendState != null) ? ((CppObject)blendState).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, float*, uint, void>)((CppObject)this)[OMSetBlendState__vtbl_index])(((CppObject)this).NativePointer, (void*)intPtr, blendFactor, sampleMask);
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState, ReadOnlySpan<float> blendFactor)
	{
		IntPtr intPtr = ((blendState != null) ? ((CppObject)blendState).NativePointer : IntPtr.Zero);
		fixed (float* ptr = blendFactor)
		{
			((delegate* unmanaged[Stdcall]<IntPtr, void*, float*, uint, void>)((CppObject)this)[OMSetBlendState__vtbl_index])(((CppObject)this).NativePointer, (void*)intPtr, ptr, uint.MaxValue);
		}
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState, Color4 blendFactor)
	{
		OMSetBlendState(blendState, (float*)(&blendFactor), uint.MaxValue);
	}

	public unsafe void OMSetBlendState(ID3D11BlendState? blendState, Color4 blendFactor, uint sampleMask = uint.MaxValue)
	{
		OMSetBlendState(blendState, (float*)(&blendFactor), sampleMask);
	}

	/// <summary>
	/// Unsets the render targets.
	/// </summary>
	public unsafe void UnsetRenderTargets()
	{
		OMSetRenderTargets(0, (void*)null, (ID3D11DepthStencilView)null);
	}

	public unsafe void OMSetRenderTargets(ID3D11RenderTargetView renderTargetView, ID3D11DepthStencilView? depthStencilView = null)
	{
		IntPtr intPtr = (((CppObject)(object)renderTargetView == (CppObject)null) ? IntPtr.Zero : ((CppObject)renderTargetView).NativePointer);
		OMSetRenderTargets(1, &intPtr, depthStencilView);
	}

	public unsafe void OMSetRenderTargets(int renderTargetViewsCount, ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView? depthStencilView = null)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViewsCount];
		for (int i = 0; i < renderTargetViewsCount; i++)
		{
			ptr[i] = (((CppObject)(object)renderTargetViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)renderTargetViews[i]).NativePointer);
		}
		OMSetRenderTargets(renderTargetViewsCount, ptr, depthStencilView);
	}

	public unsafe void OMSetRenderTargets(ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView? depthStencilView = null)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViews.Length];
		for (int i = 0; i < renderTargetViews.Length; i++)
		{
			ptr[i] = (((CppObject)(object)renderTargetViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)renderTargetViews[i]).NativePointer);
		}
		OMSetRenderTargets(renderTargetViews.Length, ptr, depthStencilView);
	}

	public unsafe void OMSetRenderTargets(ReadOnlySpan<ID3D11RenderTargetView> renderTargetViews, ID3D11DepthStencilView? depthStencilView = null)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViews.Length];
		for (int i = 0; i < renderTargetViews.Length; i++)
		{
			ptr[i] = (((CppObject)(object)renderTargetViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)renderTargetViews[i]).NativePointer);
		}
		OMSetRenderTargets(renderTargetViews.Length, ptr, depthStencilView);
	}

	public unsafe void OMSetUnorderedAccessView(int startSlot, ID3D11UnorderedAccessView unorderedAccessView, int uavInitialCount = -1)
	{
		IntPtr intPtr = (((CppObject)(object)unorderedAccessView != (CppObject)null) ? ((CppObject)unorderedAccessView).NativePointer : IntPtr.Zero);
		OMSetRenderTargetsAndUnorderedAccessViews(-1, null, IntPtr.Zero, startSlot, 1, &intPtr, &uavInitialCount);
	}

	public unsafe void OMUnsetUnorderedAccessView(int startSlot, int uavInitialCount = -1)
	{
		void* ptr = default(void*);
		OMSetRenderTargetsAndUnorderedAccessViews(-1, null, IntPtr.Zero, startSlot, 1, &ptr, &uavInitialCount);
	}

	public unsafe void OMSetUnorderedAccessViews(int uavStartSlot, int unorderedAccessViewCount, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		IntPtr* ptr = stackalloc IntPtr[unorderedAccessViewCount];
		for (int i = 0; i < unorderedAccessViewCount; i++)
		{
			ptr[i] = ((CppObject)unorderedAccessViews[i]).NativePointer;
		}
		fixed (int* unorderedAccessViewInitialCounts = &s_NegativeOnes[0])
		{
			OMSetRenderTargetsAndUnorderedAccessViews(-1, null, IntPtr.Zero, uavStartSlot, unorderedAccessViewCount, ptr, unorderedAccessViewInitialCounts);
		}
	}

	public unsafe void OMSetRenderTargetsAndUnorderedAccessViews(ID3D11RenderTargetView renderTargetView, ID3D11DepthStencilView depthStencilView, int startSlot, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		IntPtr intPtr = (((CppObject)(object)renderTargetView == (CppObject)null) ? IntPtr.Zero : ((CppObject)renderTargetView).NativePointer);
		IntPtr* ptr = stackalloc IntPtr[unorderedAccessViews.Length];
		int* ptr2 = stackalloc int[unorderedAccessViews.Length];
		for (int i = 0; i < unorderedAccessViews.Length; i++)
		{
			ptr[i] = ((CppObject)unorderedAccessViews[i]).NativePointer;
			ptr2[i] = -1;
		}
		OMSetRenderTargetsAndUnorderedAccessViews(1, &intPtr, ((CppObject)(object)depthStencilView != (CppObject)null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero, startSlot, unorderedAccessViews.Length, ptr, ptr2);
	}

	public unsafe void OMSetRenderTargetsAndUnorderedAccessViews(ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView depthStencilView, int startSlot, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViews.Length];
		for (int i = 0; i < renderTargetViews.Length; i++)
		{
			ptr[i] = ((CppObject)renderTargetViews[i]).NativePointer;
		}
		IntPtr* ptr2 = stackalloc IntPtr[unorderedAccessViews.Length];
		int* ptr3 = stackalloc int[unorderedAccessViews.Length];
		for (int j = 0; j < unorderedAccessViews.Length; j++)
		{
			ptr2[j] = ((CppObject)unorderedAccessViews[j]).NativePointer;
			ptr3[j] = -1;
		}
		OMSetRenderTargetsAndUnorderedAccessViews(renderTargetViews.Length, ptr, ((CppObject)(object)depthStencilView != (CppObject)null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero, startSlot, unorderedAccessViews.Length, ptr2, ptr3);
	}

	public void OMSetRenderTargetsAndUnorderedAccessViews(ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView depthStencilView, int uavStartSlot, ID3D11UnorderedAccessView[] unorderedAccessViews, int[] uavInitialCounts)
	{
		OMSetRenderTargetsAndUnorderedAccessViews(renderTargetViews.Length, renderTargetViews, depthStencilView, uavStartSlot, unorderedAccessViews.Length, unorderedAccessViews, uavInitialCounts);
	}

	public unsafe void OMSetRenderTargetsAndUnorderedAccessViews(int renderTargetViewsCount, ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView depthStencilView, int startSlot, int unorderedAccessViewsCount, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViews.Length];
		for (int i = 0; i < renderTargetViews.Length; i++)
		{
			ptr[i] = ((CppObject)renderTargetViews[i]).NativePointer;
		}
		IntPtr* ptr2 = stackalloc IntPtr[unorderedAccessViews.Length];
		for (int j = 0; j < unorderedAccessViews.Length; j++)
		{
			ptr2[j] = ((CppObject)unorderedAccessViews[j]).NativePointer;
		}
		fixed (int* unorderedAccessViewInitialCounts = &s_NegativeOnes[0])
		{
			OMSetRenderTargetsAndUnorderedAccessViews(renderTargetViewsCount, ptr, ((CppObject)(object)depthStencilView != (CppObject)null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero, startSlot, unorderedAccessViewsCount, ptr2, unorderedAccessViewInitialCounts);
		}
	}

	public unsafe void OMSetRenderTargetsAndUnorderedAccessViews(int renderTargetViewsCount, ID3D11RenderTargetView[] renderTargetViews, ID3D11DepthStencilView depthStencilView, int startSlot, int unorderedAccessViewsCount, ID3D11UnorderedAccessView[] unorderedAccessViews, int[] uavInitialCounts)
	{
		IntPtr* ptr = stackalloc IntPtr[renderTargetViews.Length];
		for (int i = 0; i < renderTargetViews.Length; i++)
		{
			ptr[i] = ((CppObject)renderTargetViews[i]).NativePointer;
		}
		IntPtr* ptr2 = stackalloc IntPtr[unorderedAccessViews.Length];
		for (int j = 0; j < unorderedAccessViews.Length; j++)
		{
			ptr2[j] = ((CppObject)unorderedAccessViews[j]).NativePointer;
		}
		fixed (int* unorderedAccessViewInitialCounts = &uavInitialCounts[0])
		{
			OMSetRenderTargetsAndUnorderedAccessViews(renderTargetViewsCount, ptr, ((CppObject)(object)depthStencilView != (CppObject)null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero, startSlot, unorderedAccessViewsCount, ptr2, unorderedAccessViewInitialCounts);
		}
	}

	public ID3D11CommandList FinishCommandList(bool restoreState)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		ID3D11CommandList commandList;
		Result val = FinishCommandList(RawBool.op_Implicit(restoreState), out commandList);
		((Result)(ref val)).CheckError();
		return commandList;
	}

	public bool IsDataAvailable(ID3D11Asynchronous data)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return GetData(data, IntPtr.Zero, 0, AsyncGetDataFlags.None) == Result.Ok;
	}

	public bool IsDataAvailable(ID3D11Asynchronous data, AsyncGetDataFlags flags)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return GetData(data, IntPtr.Zero, 0, flags) == Result.Ok;
	}

	/// <summary>
	///   Gets data from the GPU asynchronously.
	/// </summary>
	/// <param name="data">The asynchronous data provider.</param>
	/// <param name="flags">Flags specifying how the command should operate.</param>
	/// <returns>The data retrieved from the GPU.</returns>
	public DataStream GetData(ID3D11Asynchronous data, AsyncGetDataFlags flags = AsyncGetDataFlags.None)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		DataStream val = new DataStream(data.DataSize, true, true);
		GetData(data, val.BasePointer, (int)((Stream)(object)val).Length, flags);
		return val;
	}

	public T GetData<T>(ID3D11Asynchronous data, AsyncGetDataFlags flags) where T : unmanaged
	{
		GetData<T>(data, flags, out var result);
		return result;
	}

	public unsafe bool GetData<T>(ID3D11Asynchronous data, AsyncGetDataFlags flags, out T result) where T : unmanaged
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		result = default(T);
		fixed (T* ptr = &result)
		{
			void* ptr2 = ptr;
			return GetData(data, (IntPtr)ptr2, sizeof(T), flags) == Result.Ok;
		}
	}

	public unsafe bool GetData<T>(ID3D11Asynchronous data, out T result) where T : unmanaged
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		result = default(T);
		fixed (T* ptr = &result)
		{
			void* ptr2 = ptr;
			return GetData(data, (IntPtr)ptr2, sizeof(T), AsyncGetDataFlags.None) == Result.Ok;
		}
	}

	public unsafe void OMGetBlendState(out ID3D11BlendState blendState, float* blendFactor, out uint sampleMask)
	{
		IntPtr zero = IntPtr.Zero;
		((delegate* unmanaged[Stdcall]<IntPtr, void*, float*, out uint, void>)((CppObject)this)[OMGetBlendState__vtbl_index])(((CppObject)this).NativePointer, &zero, blendFactor, out sampleMask);
		blendState = new ID3D11BlendState(zero);
	}

	public unsafe ID3D11BlendState OMGetBlendState()
	{
		OMGetBlendState(out ID3D11BlendState blendState, null, out uint _);
		return blendState;
	}

	public unsafe ID3D11BlendState OMGetBlendState(out Color4 blendFactor)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		Color4 val = default(Color4);
		OMGetBlendState(out ID3D11BlendState blendState, (float*)(&val), out uint _);
		blendFactor = val;
		return blendState;
	}

	public unsafe ID3D11BlendState OMGetBlendState(out Color4 blendFactor, out uint sampleMask)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		Color4 val = default(Color4);
		OMGetBlendState(out ID3D11BlendState blendState, (float*)(&val), out sampleMask);
		blendFactor = val;
		return blendState;
	}

	public unsafe void RSSetViewport(float x, float y, float width, float height, float minDepth = 0f, float maxDepth = 1f)
	{
		Viewport val = default(Viewport);
		((Viewport)(ref val))..ctor(x, y, width, height, minDepth, maxDepth);
		RSSetViewports(1, &val);
	}

	public unsafe void RSSetViewport(Viewport viewport)
	{
		RSSetViewports(1, &viewport);
	}

	public unsafe void RSSetViewports(Viewport[] viewports)
	{
		fixed (Viewport* viewports2 = viewports)
		{
			RSSetViewports(viewports.Length, viewports2);
		}
	}

	public unsafe void RSSetViewports(int count, Viewport[] viewports)
	{
		fixed (Viewport* viewports2 = viewports)
		{
			RSSetViewports(count, viewports2);
		}
	}

	public unsafe void RSSetViewports(Span<Viewport> viewports)
	{
		fixed (Viewport* viewports2 = viewports)
		{
			RSSetViewports(viewports.Length, viewports2);
		}
	}

	public unsafe void RSSetViewport<T>(T viewport) where T : unmanaged
	{
		RSSetViewports(1, &viewport);
	}

	public unsafe void RSSetViewports<T>(T[] viewports) where T : unmanaged
	{
		fixed (T* viewports2 = &viewports[0])
		{
			RSSetViewports(viewports.Length, viewports2);
		}
	}

	public unsafe void RSSetViewports<T>(Span<T> viewports) where T : unmanaged
	{
		fixed (T* viewports2 = viewports)
		{
			RSSetViewports(viewports.Length, viewports2);
		}
	}

	/// <summary>
	/// Get the number of bound viewports.
	/// </summary>
	/// <returns></returns>
	public unsafe int RSGetViewports()
	{
		int numViewports = 0;
		RSGetViewports(ref numViewports, (void*)null);
		return numViewports;
	}

	public unsafe Viewport RSGetViewport()
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		int numViewports = 1;
		Viewport result = default(Viewport);
		RSGetViewports(ref numViewports, (void*)(&result));
		return result;
	}

	public unsafe void RSGetViewport(ref Viewport viewport)
	{
		int count = 1;
		fixed (Viewport* viewports = &viewport)
		{
			RSGetViewports(ref count, viewports);
		}
	}

	public unsafe void RSGetViewports(Viewport[] viewports)
	{
		int numViewports = viewports.Length;
		fixed (Viewport* viewports2 = &viewports[0])
		{
			RSGetViewports(ref numViewports, (void*)viewports2);
		}
	}

	public unsafe void RSGetViewports(Span<Viewport> viewports)
	{
		fixed (Viewport* reference = &MemoryMarshal.GetReference(viewports))
		{
			int numViewports = viewports.Length;
			RSGetViewports(ref numViewports, (void*)reference);
		}
	}

	public unsafe void RSGetViewports<T>(ref int count, T[] viewports) where T : unmanaged
	{
		fixed (T* viewports2 = &viewports[0])
		{
			RSGetViewports(ref count, viewports2);
		}
	}

	public unsafe void RSGetViewports<T>(Span<T> viewports) where T : unmanaged
	{
		fixed (T* viewports2 = viewports)
		{
			int numViewports = viewports.Length;
			RSGetViewports(ref numViewports, viewports2);
		}
	}

	/// <summary>
	/// Get the array of viewports bound  to the rasterizer stage.
	/// </summary>
	/// <typeparam name="T">An array of viewports,  must be size of <see cref="T:Vortice.Mathematics.Viewport" />.</typeparam>
	/// <param name="viewports"></param>
	public unsafe void RSGetViewports<T>(T[] viewports) where T : unmanaged
	{
		int numViewports = viewports.Length;
		fixed (T* viewports2 = &viewports[0])
		{
			RSGetViewports(ref numViewports, viewports2);
		}
	}

	/// <summary>	
	/// Get the array of viewports bound  to the rasterizer stage.	
	/// </summary>	
	/// <returns>An array of viewports, must be size of <see cref="T:Vortice.Mathematics.Viewport" /></returns>
	public unsafe T[] RSGetViewports<T>() where T : unmanaged
	{
		int numViewports = 0;
		RSGetViewports(ref numViewports, (void*)null);
		T[] array = new T[numViewports];
		RSGetViewports(array);
		return array;
	}

	public unsafe void RSGetViewports(ref int count, Viewport[] viewports)
	{
		fixed (Viewport* viewports2 = &viewports[0])
		{
			RSGetViewports(ref count, (void*)viewports2);
		}
	}

	public unsafe void RSGetViewports(ref int count, Span<Viewport> viewports)
	{
		fixed (Viewport* reference = &MemoryMarshal.GetReference(viewports))
		{
			RSGetViewports(ref count, (void*)reference);
		}
	}

	public unsafe void RSGetViewports(ref int count, Viewport* viewports)
	{
		RSGetViewports(ref count, (void*)viewports);
	}

	/// <summary>
	/// Get the number of bound scissor rectangles.
	/// </summary>
	/// <returns></returns>
	public int RSGetScissorRects()
	{
		int numRects = 0;
		RSGetScissorRects(ref numRects, IntPtr.Zero);
		return numRects;
	}

	public unsafe void RSSetScissorRect(int x, int y, int width, int height)
	{
		RawRect val = default(RawRect);
		((RawRect)(ref val))..ctor(x, y, x + width, y + height);
		RSSetScissorRects(1, &val);
	}

	public unsafe void RSSetScissorRect(int width, int height)
	{
		RawRect val = default(RawRect);
		((RawRect)(ref val))..ctor(0, 0, width, height);
		RSSetScissorRects(1, &val);
	}

	public unsafe void RSSetScissorRect(in Rectangle rectangle)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		RawRect val = RawRect.op_Implicit(rectangle);
		RSSetScissorRects(1, &val);
	}

	public unsafe void RSSetScissorRect(RawRect rectangle)
	{
		RSSetScissorRects(1, &rectangle);
	}

	public unsafe void RSSetScissorRects(RawRect[] rectangles)
	{
		fixed (RawRect* rects = rectangles)
		{
			RSSetScissorRects(rectangles.Length, rects);
		}
	}

	public unsafe void RSSetScissorRects(int count, RawRect[] rectangles)
	{
		fixed (RawRect* rects = rectangles)
		{
			RSSetScissorRects(count, rects);
		}
	}

	public unsafe void RSSetScissorRects(Span<RawRect> rectangles)
	{
		fixed (RawRect* rects = rectangles)
		{
			RSSetScissorRects(rectangles.Length, rects);
		}
	}

	public unsafe void RSSetScissorRects(int count, Span<RawRect> rectangles)
	{
		fixed (RawRect* rects = rectangles)
		{
			RSSetScissorRects(count, rects);
		}
	}

	public unsafe void RSSetScissorRect<T>(T rect) where T : unmanaged
	{
		RSSetScissorRects(1, &rect);
	}

	public unsafe void RSSetScissorRects<T>(T[] rects) where T : unmanaged
	{
		fixed (T* ptr = &rects[0])
		{
			void* rects2 = ptr;
			RSSetScissorRects(rects.Length, rects2);
		}
	}

	public unsafe void RSSetScissorRects<T>(int numRects, T[] rects) where T : unmanaged
	{
		fixed (T* ptr = &rects[0])
		{
			void* rects2 = ptr;
			RSSetScissorRects(numRects, rects2);
		}
	}

	public unsafe void RSSetScissorRects<T>(Span<T> rects) where T : unmanaged
	{
		fixed (T* ptr = rects)
		{
			void* rects2 = ptr;
			RSSetScissorRects(rects.Length, rects2);
		}
	}

	public unsafe RawRect RSGetScissorRect()
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		int numRects = 1;
		RawRect result = default(RawRect);
		RSGetScissorRects(ref numRects, new IntPtr(&result));
		return result;
	}

	public unsafe void RSGetScissorRect(ref RawRect rect)
	{
		int numRects = 1;
		fixed (RawRect* ptr = &rect)
		{
			void* ptr2 = ptr;
			RSGetScissorRects(ref numRects, (IntPtr)ptr2);
		}
	}

	public unsafe void RSGetScissorRects(RawRect[] rects)
	{
		int numRects = rects.Length;
		fixed (RawRect* ptr = &rects[0])
		{
			void* ptr2 = ptr;
			RSGetScissorRects(ref numRects, (IntPtr)ptr2);
		}
	}

	public unsafe void RSGetScissorRects(Span<RawRect> rects)
	{
		fixed (RawRect* reference = &MemoryMarshal.GetReference(rects))
		{
			int numRects = rects.Length;
			RSGetScissorRects(ref numRects, (IntPtr)reference);
		}
	}

	public unsafe void RSGetScissorRects(ref int count, RawRect[] rects)
	{
		fixed (RawRect* ptr = &rects[0])
		{
			void* ptr2 = ptr;
			RSGetScissorRects(ref count, (IntPtr)ptr2);
		}
	}

	public unsafe void RSGetScissorRects(ref int count, RawRect* rects)
	{
		RSGetScissorRects(ref count, (IntPtr)rects);
	}

	/// <summary>
	/// Set the target output buffers for the stream-output stage of the pipeline.
	/// </summary>
	/// <param name="targets">The array of output buffers <see cref="T:Vortice.Direct3D11.ID3D11Buffer" /> to bind to the device. The buffers must have been created with the <see cref="F:Vortice.Direct3D11.BindFlags.StreamOutput" /> flag.</param>
	/// <param name="offsets">Array of offsets to the output buffers from targets, one offset for each buffer. The offset values must be in bytes.</param>
	public void SOSetTargets(ID3D11Buffer[] targets, int[]? offsets = null)
	{
		SOSetTargets(targets.Length, targets, offsets);
	}

	/// <summary>
	/// Set the target output buffers for the stream-output stage of the pipeline.
	/// </summary>
	/// <param name="buffersCount">The number of buffer to bind to the device. A maximum of four output buffers can be set. If less than four are defined by the call, the remaining buffer slots are set to null.</param>
	/// <param name="targets">The array of output buffers <see cref="T:Vortice.Direct3D11.ID3D11Buffer" /> to bind to the device. The buffers must have been created with the <see cref="F:Vortice.Direct3D11.BindFlags.StreamOutput" /> flag.</param>
	/// <param name="offsets">Array of offsets to the output buffers from targets, one offset for each buffer. The offset values must be in bytes.</param>
	public unsafe void SOSetTargets(int buffersCount, ID3D11Buffer[] targets, int[]? offsets = null)
	{
		IntPtr* ptr = stackalloc IntPtr[buffersCount];
		for (int i = 0; i < buffersCount; i++)
		{
			ptr[i] = (((CppObject)(object)targets[i] != (CppObject)null) ? ((CppObject)targets[i]).NativePointer : IntPtr.Zero);
		}
		if (offsets != null && offsets.Length != 0)
		{
			fixed (int* offsets2 = &offsets[0])
			{
				SOSetTargets(buffersCount, ptr, offsets2);
			}
		}
		else
		{
			SOSetTargets(buffersCount, ptr, null);
		}
	}

	/// <summary>
	/// Unsets the render targets.
	/// </summary>
	public unsafe void UnsetSOTargets()
	{
		SOSetTargets(0, (void*)null, (void*)null);
	}

	public unsafe void IASetVertexBuffer(int slot, ID3D11Buffer buffer, int stride, int offset = 0)
	{
		IntPtr intPtr = (((CppObject)(object)buffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)buffer).NativePointer);
		IASetVertexBuffers(slot, 1, &intPtr, &stride, &offset);
	}

	public void IASetVertexBuffers(int firstSlot, ID3D11Buffer[] vertexBuffers, int[] strides, int[] offsets)
	{
		IASetVertexBuffers(firstSlot, vertexBuffers.Length, vertexBuffers, strides, offsets);
	}

	public unsafe void IASetVertexBuffers(int firstSlot, int vertexBufferViewsCount, ID3D11Buffer[] vertexBuffers, int[] strides, int[] offsets)
	{
		IntPtr* ptr = stackalloc IntPtr[vertexBufferViewsCount];
		for (int i = 0; i < vertexBufferViewsCount; i++)
		{
			ptr[i] = (((CppObject)(object)vertexBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)vertexBuffers[i]).NativePointer);
		}
		fixed (int* strides2 = strides)
		{
			fixed (int* offsets2 = offsets)
			{
				IASetVertexBuffers(firstSlot, vertexBufferViewsCount, ptr, strides2, offsets2);
			}
		}
	}

	public unsafe void IASetVertexBuffers(int firstSlot, int vertexBufferViewsCount, ID3D11Buffer[] vertexBuffers, Span<int> strides, Span<int> offsets)
	{
		IntPtr* ptr = stackalloc IntPtr[vertexBufferViewsCount];
		for (int i = 0; i < vertexBufferViewsCount; i++)
		{
			ptr[i] = (((CppObject)(object)vertexBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)vertexBuffers[i]).NativePointer);
		}
		fixed (int* strides2 = strides)
		{
			fixed (int* offsets2 = offsets)
			{
				IASetVertexBuffers(firstSlot, vertexBufferViewsCount, ptr, strides2, offsets2);
			}
		}
	}

	public void VSSetShader(ID3D11VertexShader? vertexShader)
	{
		IntPtr vertexShader2 = ((vertexShader != null) ? ((CppObject)vertexShader).NativePointer : IntPtr.Zero);
		VSSetShader(vertexShader2, IntPtr.Zero, 0);
	}

	public void VSSetShader(ID3D11VertexShader? vertexShader, ID3D11ClassInstance[] classInstances)
	{
		VSSetShader(vertexShader, classInstances, classInstances.Length);
	}

	public unsafe void VSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		VSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void VSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			VSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void VSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		VSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void VSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		VSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void VSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		VSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void VSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		VSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void VSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			VSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void VSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		VSSetSamplers(slot, 1, &intPtr);
	}

	public void VSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		VSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void VSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		VSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void VSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		VSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void VSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		VSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void VSSetShaderResource(int slot, ID3D11ShaderResourceView? shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		VSSetShaderResources(slot, 1, &intPtr);
	}

	public void VSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		VSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void VSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		VSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11VertexShader VSGetShader()
	{
		int numClassInstances = 0;
		VSGetShader(out var vertexShader, null, ref numClassInstances);
		return vertexShader;
	}

	public ID3D11VertexShader VSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		VSGetShader(out var vertexShader, classInstances, ref numClassInstances);
		return vertexShader;
	}

	public ID3D11VertexShader VSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		VSGetShader(out var vertexShader, classInstances, ref classInstancesCount);
		return vertexShader;
	}

	public void VSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		VSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void VSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		VSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void VSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		VSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public void PSSetShader(ID3D11PixelShader? pixelShader)
	{
		IntPtr pixelShader2 = ((pixelShader != null) ? ((CppObject)pixelShader).NativePointer : IntPtr.Zero);
		PSSetShader(pixelShader2, IntPtr.Zero, 0);
	}

	public void PSSetShader(ID3D11PixelShader? pixelShader, ID3D11ClassInstance[] classInstances)
	{
		PSSetShader(pixelShader, classInstances, classInstances.Length);
	}

	public unsafe void PSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		PSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void PSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			PSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void PSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		PSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void PSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		PSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void PSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		PSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void PSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		PSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void PSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			PSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void PSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		PSSetSamplers(slot, 1, &intPtr);
	}

	public void PSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		PSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void PSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		PSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void PSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		PSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void PSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		PSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void PSSetShaderResource(int slot, ID3D11ShaderResourceView shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		PSSetShaderResources(slot, 1, &intPtr);
	}

	public void PSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		PSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void PSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		PSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11PixelShader PSGetShader()
	{
		int numClassInstances = 0;
		PSGetShader(out var pixelShader, null, ref numClassInstances);
		return pixelShader;
	}

	public ID3D11PixelShader PSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		PSGetShader(out var pixelShader, classInstances, ref numClassInstances);
		return pixelShader;
	}

	public ID3D11PixelShader PSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		PSGetShader(out var pixelShader, classInstances, ref classInstancesCount);
		return pixelShader;
	}

	public void PSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		PSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void PSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		PSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void PSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		PSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public void DSSetShader(ID3D11DomainShader? domainShader)
	{
		IntPtr domainShader2 = ((domainShader != null) ? ((CppObject)domainShader).NativePointer : IntPtr.Zero);
		DSSetShader(domainShader2, IntPtr.Zero, 0);
	}

	public void DSSetShader(ID3D11DomainShader? domainShader, ID3D11ClassInstance[] classInstances)
	{
		DSSetShader(domainShader, classInstances, classInstances.Length);
	}

	public unsafe void DSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		DSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void DSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			DSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void DSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		DSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void DSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		DSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void DSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		DSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void DSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		DSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void DSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			DSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void DSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		DSSetSamplers(slot, 1, &intPtr);
	}

	public void DSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		DSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void DSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		DSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void DSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		DSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void DSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		DSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void DSSetShaderResource(int slot, ID3D11ShaderResourceView? shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		DSSetShaderResources(slot, 1, &intPtr);
	}

	public void DSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		DSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void DSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		DSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11DomainShader DSGetShader()
	{
		int numClassInstances = 0;
		DSGetShader(out var domainShader, null, ref numClassInstances);
		return domainShader;
	}

	public ID3D11DomainShader DSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		DSGetShader(out var domainShader, classInstances, ref numClassInstances);
		return domainShader;
	}

	public ID3D11DomainShader DSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		DSGetShader(out var domainShader, classInstances, ref classInstancesCount);
		return domainShader;
	}

	public void DSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		DSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void DSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		DSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void DSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		DSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public void HSSetShader(ID3D11HullShader? hullShader)
	{
		IntPtr hullShader2 = ((hullShader != null) ? ((CppObject)hullShader).NativePointer : IntPtr.Zero);
		HSSetShader(hullShader2, IntPtr.Zero, 0);
	}

	public void HSSetShader(ID3D11HullShader? hullShader, ID3D11ClassInstance[] classInstances)
	{
		HSSetShader(hullShader, classInstances, classInstances.Length);
	}

	public unsafe void HSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		HSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void HSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			HSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void HSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		HSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void HSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		HSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void HSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		HSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void HSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		HSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void HSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			HSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void HSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		HSSetSamplers(slot, 1, &intPtr);
	}

	public void HSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		HSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void HSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		HSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void HSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		HSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void HSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		HSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void HSSetShaderResource(int slot, ID3D11ShaderResourceView? shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		HSSetShaderResources(slot, 1, &intPtr);
	}

	public void HSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		HSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void HSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		HSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11HullShader HSGetShader()
	{
		int numClassInstances = 0;
		HSGetShader(out var hullShader, null, ref numClassInstances);
		return hullShader;
	}

	public ID3D11HullShader HSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		HSGetShader(out var hullShader, classInstances, ref numClassInstances);
		return hullShader;
	}

	public ID3D11HullShader HSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		HSGetShader(out var hullShader, classInstances, ref classInstancesCount);
		return hullShader;
	}

	public void HSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		HSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void HSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		HSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void HSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		HSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public void GSSetShader(ID3D11GeometryShader? geometryShader)
	{
		IntPtr shader = ((geometryShader != null) ? ((CppObject)geometryShader).NativePointer : IntPtr.Zero);
		GSSetShader(shader, IntPtr.Zero, 0);
	}

	public void GSSetShader(ID3D11GeometryShader? geometryShader, ID3D11ClassInstance[] classInstances)
	{
		GSSetShader(geometryShader, classInstances, classInstances.Length);
	}

	public unsafe void GSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		GSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void GSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			GSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void GSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		GSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void GSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		GSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void GSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		GSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void GSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		GSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void GSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			GSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void GSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		GSSetSamplers(slot, 1, &intPtr);
	}

	public void GSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		GSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void GSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		GSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void GSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		GSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void GSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		GSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void GSSetShaderResource(int slot, ID3D11ShaderResourceView? shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		GSSetShaderResources(slot, 1, &intPtr);
	}

	public void GSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		GSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void GSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		GSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11GeometryShader GSGetShader()
	{
		int numClassInstances = 0;
		GSGetShader(out var geometryShader, null, ref numClassInstances);
		return geometryShader;
	}

	public ID3D11GeometryShader GSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		GSGetShader(out var geometryShader, classInstances, ref numClassInstances);
		return geometryShader;
	}

	public ID3D11GeometryShader GSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		GSGetShader(out var geometryShader, classInstances, ref classInstancesCount);
		return geometryShader;
	}

	public void GSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		GSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void GSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		GSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void GSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		GSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public void CSSetShader(ID3D11ComputeShader? computeShader)
	{
		IntPtr computeShader2 = ((computeShader != null) ? ((CppObject)computeShader).NativePointer : IntPtr.Zero);
		CSSetShader(computeShader2, IntPtr.Zero, 0);
	}

	public void CSSetShader(ID3D11ComputeShader? computeShader, ID3D11ClassInstance[] classInstances)
	{
		CSSetShader(computeShader, classInstances, classInstances.Length);
	}

	public unsafe void CSUnsetConstantBuffer(int slot)
	{
		void* ptr = default(void*);
		CSSetConstantBuffers(slot, 1, &ptr);
	}

	public unsafe void CSUnsetConstantBuffers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullBuffers)
		{
			void* constantBuffers = ptr;
			CSSetConstantBuffers(startSlot, count, constantBuffers);
		}
	}

	public unsafe void CSSetConstantBuffer(int slot, ID3D11Buffer? constantBuffer)
	{
		IntPtr intPtr = (((CppObject)(object)constantBuffer == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffer).NativePointer);
		CSSetConstantBuffers(slot, 1, &intPtr);
	}

	public void CSSetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		CSSetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public unsafe void CSSetConstantBuffers(int startSlot, int count, ID3D11Buffer[] constantBuffers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)constantBuffers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)constantBuffers[i]).NativePointer);
		}
		CSSetConstantBuffers(startSlot, count, ptr);
	}

	public unsafe void CSUnsetSampler(int slot)
	{
		void* ptr = default(void*);
		CSSetSamplers(slot, 1, &ptr);
	}

	public unsafe void CSUnsetSamplers(int startSlot, int count)
	{
		fixed (void** ptr = s_NullSamplers)
		{
			void* samplers = ptr;
			CSSetSamplers(startSlot, count, samplers);
		}
	}

	public unsafe void CSSetSampler(int slot, ID3D11SamplerState? sampler)
	{
		IntPtr intPtr = (((CppObject)(object)sampler == (CppObject)null) ? IntPtr.Zero : ((CppObject)sampler).NativePointer);
		CSSetSamplers(slot, 1, &intPtr);
	}

	public void CSSetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		CSSetSamplers(startSlot, samplers.Length, samplers);
	}

	public unsafe void CSSetSamplers(int startSlot, int count, ID3D11SamplerState[] samplers)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)samplers[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)samplers[i]).NativePointer);
		}
		CSSetSamplers(startSlot, count, ptr);
	}

	public unsafe void CSUnsetShaderResource(int slot)
	{
		void* ptr = default(void*);
		CSSetShaderResources(slot, 1, &ptr);
	}

	public unsafe void CSUnsetShaderResources(int startSlot, int count)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = IntPtr.Zero;
		}
		CSSetShaderResources(startSlot, count, ptr);
	}

	public unsafe void CSSetShaderResource(int slot, ID3D11ShaderResourceView? shaderResourceView)
	{
		IntPtr intPtr = (((CppObject)(object)shaderResourceView == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceView).NativePointer);
		CSSetShaderResources(slot, 1, &intPtr);
	}

	public void CSSetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		CSSetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void CSSetShaderResources(int startSlot, int count, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		IntPtr* ptr = stackalloc IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			ptr[i] = (((CppObject)(object)shaderResourceViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)shaderResourceViews[i]).NativePointer);
		}
		CSSetShaderResources(startSlot, count, ptr);
	}

	public ID3D11ComputeShader CSGetShader()
	{
		int numClassInstances = 0;
		CSGetShader(out var computeShader, null, ref numClassInstances);
		return computeShader;
	}

	public ID3D11ComputeShader CSGetShader(ID3D11ClassInstance[] classInstances)
	{
		int numClassInstances = classInstances.Length;
		CSGetShader(out var computeShader, classInstances, ref numClassInstances);
		return computeShader;
	}

	public ID3D11ComputeShader CSGetShader(ref int classInstancesCount, ID3D11ClassInstance[] classInstances)
	{
		CSGetShader(out var computeShader, classInstances, ref classInstancesCount);
		return computeShader;
	}

	public void CSGetConstantBuffers(int startSlot, ID3D11Buffer[] constantBuffers)
	{
		CSGetConstantBuffers(startSlot, constantBuffers.Length, constantBuffers);
	}

	public void CSGetSamplers(int startSlot, ID3D11SamplerState[] samplers)
	{
		CSGetSamplers(startSlot, samplers.Length, samplers);
	}

	public void CSGetShaderResources(int startSlot, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		CSGetShaderResources(startSlot, shaderResourceViews.Length, shaderResourceViews);
	}

	public unsafe void CSSetUnorderedAccessView(int slot, ID3D11UnorderedAccessView? unorderedAccessView, int uavInitialCount = -1)
	{
		IntPtr intPtr = (((CppObject)(object)unorderedAccessView == (CppObject)null) ? IntPtr.Zero : ((CppObject)unorderedAccessView).NativePointer);
		CSSetUnorderedAccessViews(slot, 1, &intPtr, &uavInitialCount);
	}

	public unsafe void CSSetUnorderedAccessViews(int startSlot, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		int num = unorderedAccessViews.Length;
		IntPtr* ptr = stackalloc IntPtr[num];
		int* ptr2 = stackalloc int[num];
		for (int i = 0; i < num; i++)
		{
			ptr[i] = (((CppObject)(object)unorderedAccessViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)unorderedAccessViews[i]).NativePointer);
			ptr2[i] = -1;
		}
		CSSetUnorderedAccessViews(startSlot, num, ptr, ptr2);
	}

	public unsafe void CSSetUnorderedAccessViews(int startSlot, int numUAVs, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		IntPtr* ptr = stackalloc IntPtr[numUAVs];
		int* ptr2 = stackalloc int[numUAVs];
		for (int i = 0; i < numUAVs; i++)
		{
			ptr[i] = (((CppObject)(object)unorderedAccessViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)unorderedAccessViews[i]).NativePointer);
			ptr2[i] = -1;
		}
		CSSetUnorderedAccessViews(startSlot, numUAVs, ptr, ptr2);
	}

	public unsafe void CSSetUnorderedAccessViews(int startSlot, ID3D11UnorderedAccessView[] unorderedAccessViews, int[] uavInitialCounts)
	{
		int num = unorderedAccessViews.Length;
		IntPtr* ptr = stackalloc IntPtr[num];
		for (int i = 0; i < num; i++)
		{
			ptr[i] = (((CppObject)(object)unorderedAccessViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)unorderedAccessViews[i]).NativePointer);
		}
		fixed (int* unorderedAccessViewInitialCounts = uavInitialCounts)
		{
			CSSetUnorderedAccessViews(startSlot, num, ptr, unorderedAccessViewInitialCounts);
		}
	}

	public unsafe void CSSetUnorderedAccessViews(int startSlot, int numUAVs, ID3D11UnorderedAccessView[] unorderedAccessViews, int[] uavInitialCounts)
	{
		IntPtr* ptr = stackalloc IntPtr[numUAVs];
		for (int i = 0; i < numUAVs; i++)
		{
			ptr[i] = (((CppObject)(object)unorderedAccessViews[i] == (CppObject)null) ? IntPtr.Zero : ((CppObject)unorderedAccessViews[i]).NativePointer);
		}
		fixed (int* unorderedAccessViewInitialCounts = &uavInitialCounts[0])
		{
			CSSetUnorderedAccessViews(startSlot, numUAVs, ptr, unorderedAccessViewInitialCounts);
		}
	}

	public unsafe void CSUnsetUnorderedAccessView(int slot, int uavInitialCount = -1)
	{
		void* ptr = default(void*);
		CSSetUnorderedAccessViews(slot, 1, &ptr, &uavInitialCount);
	}

	public unsafe void CSUnsetUnorderedAccessViews(int startSlot, int count, int uavInitialCount = -1)
	{
		fixed (void** ptr = s_NullUAVs)
		{
			void* unorderedAccessViews = ptr;
			CSSetUnorderedAccessViews(startSlot, count, unorderedAccessViews, &uavInitialCount);
		}
	}

	/// <summary>
	/// Maps the data contained in a subresource to a memory pointer, and denies the GPU access to that subresource.
	/// </summary>
	/// <param name="resource">The resource.</param>
	/// <param name="mode">The mode.</param>
	/// <param name="flags">The flags.</param>
	public MappedSubresource Map(ID3D11Buffer resource, MapMode mode, MapFlags flags = MapFlags.None)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		MappedSubresource mappedResource;
		Result val = Map(resource, 0, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return mappedResource;
	}

	public MappedSubresource Map(ID3D11Resource resource, int subresource, MapMode mode = MapMode.Read, MapFlags flags = MapFlags.None)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return mappedResource;
	}

	/// <summary>
	/// Maps the data contained in a subresource to a memory pointer, and denies the GPU access to that subresource.
	/// </summary>
	/// <param name="resource">The resource.</param>
	/// <param name="mipSlice">The mip slice.</param>
	/// <param name="arraySlice">The array slice.</param>
	/// <param name="mode">The mode.</param>
	/// <param name="flags">The flags.</param>
	/// <param name="subresource">The mapped subresource index.</param>
	/// <param name="mipSize">Size of the selected miplevel.</param>
	public MappedSubresource Map(ID3D11Resource resource, int mipSlice, int arraySlice, MapMode mode, MapFlags flags, out int subresource, out int mipSize)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return mappedResource;
	}

	/// <summary>
	/// Maps the data contained in a subresource to a memory pointer, and denies the GPU access to that subresource.
	/// </summary>
	/// <param name="resource">The resource.</param>
	/// <param name="mipSlice">The mip slice.</param>
	/// <param name="arraySlice">The array slice.</param>
	/// <param name="mode">The mode.</param>
	/// <param name="flags">The flags.</param>
	/// <param name="mipSize">Size of the selected miplevel.</param>
	/// <param name="mappedSubresource"></param>
	public Result Map(ID3D11Resource resource, int mipSlice, int arraySlice, MapMode mode, MapFlags flags, out int mipSize, out MappedSubresource mappedSubresource)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		int subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		return Map(resource, subresource, mode, flags, out mappedSubresource);
	}

	public unsafe Span<T> Map<T>(ID3D11Texture1D resource, int mipSlice, int arraySlice, MapMode mode = MapMode.Read, MapFlags flags = MapFlags.None) where T : unmanaged
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		int subresource = D3D11.CalculateSubResourceIndex(mipSlice, arraySlice, resource.GetDescription().MipLevels);
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return MemoryMarshal.Cast<byte, T>(new Span<byte>(mappedResource.DataPointer.ToPointer(), mappedResource.RowPitch));
	}

	public unsafe Span<T> Map<T>(ID3D11Texture2D resource, int mipSlice, int arraySlice, MapMode mode = MapMode.Read, MapFlags flags = MapFlags.None) where T : unmanaged
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		int mipSize;
		int subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return MemoryMarshal.Cast<byte, T>(new Span<byte>(mappedResource.DataPointer.ToPointer(), mipSize * mappedResource.RowPitch));
	}

	public unsafe Span<T> Map<T>(ID3D11Texture3D resource, int mipSlice, int arraySlice, MapMode mode = MapMode.Read, MapFlags flags = MapFlags.None) where T : unmanaged
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		int mipSize;
		int subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, mode, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return MemoryMarshal.Cast<byte, T>(new Span<byte>(mappedResource.DataPointer.ToPointer(), mipSize * mappedResource.DepthPitch));
	}

	public unsafe ReadOnlySpan<T> MapReadOnly<T>(ID3D11Resource resource, int mipSlice = 0, int arraySlice = 0, MapFlags flags = MapFlags.None) where T : unmanaged
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		int mipSize;
		int subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		MappedSubresource mappedResource;
		Result val = Map(resource, subresource, MapMode.Read, flags, out mappedResource);
		((Result)(ref val)).CheckError();
		return MemoryMarshal.Cast<byte, T>(new ReadOnlySpan<byte>(mappedResource.DataPointer.ToPointer(), mipSize * mappedResource.RowPitch));
	}

	public void Unmap(ID3D11Buffer buffer)
	{
		Unmap(buffer, 0);
	}

	public void Unmap(ID3D11Resource resource, int mipSlice, int arraySlice)
	{
		int mipSize;
		int subresource = resource.CalculateSubResourceIndex(mipSlice, arraySlice, out mipSize);
		Unmap(resource, subresource);
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="value">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="region">The region</param>
	public unsafe void UpdateSubresource<T>(in T value, ID3D11Resource resource, int subresource = 0, int rowPitch = 0, int depthPitch = 0, Box? region = null) where T : unmanaged
	{
		fixed (T* ptr = &value)
		{
			UpdateSubresource(resource, subresource, region, (IntPtr)ptr, rowPitch, depthPitch);
		}
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="data">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="region">A region that defines the portion of the destination subresource to copy the resource data into. Coordinates are in bytes for buffers and in texels for textures.</param>
	public unsafe void UpdateSubresource<T>(T[] data, ID3D11Resource resource, int subresource = 0, int rowPitch = 0, int depthPitch = 0, Box? region = null) where T : unmanaged
	{
		fixed (T* ptr = data)
		{
			UpdateSubresource(resource, subresource, region, (IntPtr)ptr, rowPitch, depthPitch);
		}
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="data">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="region">A region that defines the portion of the destination subresource to copy the resource data into. Coordinates are in bytes for buffers and in texels for textures.</param>
	public unsafe void UpdateSubresource<T>(ReadOnlySpan<T> data, ID3D11Resource resource, int subresource = 0, int rowPitch = 0, int depthPitch = 0, Box? region = null) where T : unmanaged
	{
		fixed (T* ptr = data)
		{
			UpdateSubresource(resource, subresource, region, (IntPtr)ptr, rowPitch, depthPitch);
		}
	}

	public void WriteTexture<T>(ID3D11Texture1D resource, int arraySlice, int mipLevel, T[] data) where T : unmanaged
	{
		ReadOnlySpan<T> data2 = data.AsSpan();
		WriteTexture(resource, arraySlice, mipLevel, data2);
	}

	public unsafe void WriteTexture<T>(ID3D11Texture1D resource, int arraySlice, int mipLevel, ReadOnlySpan<T> data) where T : unmanaged
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Texture1DDescription description = resource.Description;
		fixed (T* ptr = data)
		{
			int dstSubresource = D3D11.CalculateSubResourceIndex(mipLevel, arraySlice, description.MipLevels);
			int srcRowPitch = default(int);
			int srcDepthPitch = default(int);
			FormatHelper.GetSurfaceInfo(description.Format, description.GetWidth(mipLevel), 1, ref srcRowPitch, ref srcDepthPitch);
			UpdateSubresource(resource, dstSubresource, null, (IntPtr)ptr, srcRowPitch, srcDepthPitch);
		}
	}

	public void WriteTexture<T>(ID3D11Texture2D resource, int arraySlice, int mipLevel, T[] data) where T : unmanaged
	{
		ReadOnlySpan<T> data2 = data.AsSpan();
		WriteTexture(resource, arraySlice, mipLevel, data2);
	}

	public unsafe void WriteTexture<T>(ID3D11Texture2D resource, int arraySlice, int mipLevel, ReadOnlySpan<T> data) where T : unmanaged
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Texture2DDescription description = resource.Description;
		fixed (T* ptr = data)
		{
			int dstSubresource = D3D11.CalculateSubResourceIndex(mipLevel, arraySlice, description.MipLevels);
			int srcRowPitch = default(int);
			int srcDepthPitch = default(int);
			FormatHelper.GetSurfaceInfo(description.Format, description.GetWidth(mipLevel), description.GetHeight(mipLevel), ref srcRowPitch, ref srcDepthPitch);
			UpdateSubresource(resource, dstSubresource, null, (IntPtr)ptr, srcRowPitch, srcDepthPitch);
		}
	}

	public unsafe void WriteTexture<T>(ID3D11Texture2D resource, int arraySlice, int mipLevel, ref T data) where T : unmanaged
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		Texture2DDescription description = resource.Description;
		fixed (T* ptr = &data)
		{
			int dstSubresource = D3D11.CalculateSubResourceIndex(mipLevel, arraySlice, description.MipLevels);
			int srcRowPitch = default(int);
			int srcDepthPitch = default(int);
			FormatHelper.GetSurfaceInfo(description.Format, description.GetWidth(mipLevel), description.GetHeight(mipLevel), ref srcRowPitch, ref srcDepthPitch);
			UpdateSubresource(resource, dstSubresource, null, (IntPtr)ptr, srcRowPitch, srcDepthPitch);
		}
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <param name="source">The source data.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="subresource">The destination subresource.</param>
	public void UpdateSubresource(MappedSubresource source, ID3D11Resource resource, int subresource = 0)
	{
		UpdateSubresource(resource, subresource, null, source.DataPointer, source.RowPitch, source.DepthPitch);
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <param name="source">The source data.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="region">The destination region within the resource.</param>
	public void UpdateSubresource(MappedSubresource source, ID3D11Resource resource, int subresource, Box region)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		UpdateSubresource(resource, subresource, region, source.DataPointer, source.RowPitch, source.DepthPitch);
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="value">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="srcBytesPerElement">The size in bytes per pixel/block element.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="isCompressedResource">if set to <c>true</c> the resource is a block/compressed resource</param>
	/// <remarks>
	/// This method is implementing the <a href="http://blogs.msdn.com/b/chuckw/archive/2010/07/28/known-issue-direct3d-11-updatesubresource-and-deferred-contexts.aspx">workaround for deferred context</a>.
	/// </remarks>
	public unsafe void UpdateSubresourceSafe<T>(ref T value, ID3D11Resource resource, int srcBytesPerElement, int subresource = 0, int rowPitch = 0, int depthPitch = 0, bool isCompressedResource = false) where T : unmanaged
	{
		fixed (T* ptr = &value)
		{
			UpdateSubresourceSafe(resource, subresource, null, (IntPtr)ptr, rowPitch, depthPitch, srcBytesPerElement, isCompressedResource);
		}
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="data">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="srcBytesPerElement">The size in bytes per pixel/block element.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="isCompressedResource">if set to <c>true</c> the resource is a block/compressed resource</param>
	/// <remarks>
	/// This method is implementing the <a href="http://blogs.msdn.com/b/chuckw/archive/2010/07/28/known-issue-direct3d-11-updatesubresource-and-deferred-contexts.aspx">workaround for deferred context</a>.
	/// </remarks>
	public unsafe void UpdateSubresourceSafe<T>(T[] data, ID3D11Resource resource, int srcBytesPerElement, int subresource = 0, int rowPitch = 0, int depthPitch = 0, bool isCompressedResource = false) where T : unmanaged
	{
		fixed (T* ptr = &data[0])
		{
			void* ptr2 = ptr;
			UpdateSubresourceSafe(resource, subresource, null, (IntPtr)ptr2, rowPitch, depthPitch, srcBytesPerElement, isCompressedResource);
		}
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <typeparam name="T">Type of the data to upload</typeparam>
	/// <param name="data">A reference to the data to upload.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="srcBytesPerElement">The size in bytes per pixel/block element.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="rowPitch">The row pitch.</param>
	/// <param name="depthPitch">The depth pitch.</param>
	/// <param name="isCompressedResource">if set to <c>true</c> the resource is a block/compressed resource</param>
	/// <remarks>
	/// This method is implementing the <a href="http://blogs.msdn.com/b/chuckw/archive/2010/07/28/known-issue-direct3d-11-updatesubresource-and-deferred-contexts.aspx">workaround for deferred context</a>.
	/// </remarks>
	public unsafe void UpdateSubresourceSafe<T>(Span<T> data, ID3D11Resource resource, int srcBytesPerElement, int subresource = 0, int rowPitch = 0, int depthPitch = 0, bool isCompressedResource = false) where T : unmanaged
	{
		fixed (T* ptr = data)
		{
			void* ptr2 = ptr;
			UpdateSubresourceSafe(resource, subresource, null, (IntPtr)ptr2, rowPitch, depthPitch, srcBytesPerElement, isCompressedResource);
		}
	}

	/// <summary>
	///   Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <param name="source">The source data.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="srcBytesPerElement">The size in bytes per pixel/block element.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="isCompressedResource">if set to <c>true</c> the resource is a block/compressed resource</param>
	/// <remarks>
	/// This method is implementing the <a href="http://blogs.msdn.com/b/chuckw/archive/2010/07/28/known-issue-direct3d-11-updatesubresource-and-deferred-contexts.aspx">workaround for deferred context</a>.
	/// </remarks>
	public void UpdateSubresourceSafe(MappedSubresource source, ID3D11Resource resource, int srcBytesPerElement, int subresource = 0, bool isCompressedResource = false)
	{
		UpdateSubresourceSafe(resource, subresource, null, source.DataPointer, source.RowPitch, source.DepthPitch, srcBytesPerElement, isCompressedResource);
	}

	/// <summary>
	/// Copies data from the CPU to to a non-mappable subresource region.
	/// </summary>
	/// <param name="source">The source data.</param>
	/// <param name="resource">The destination resource.</param>
	/// <param name="srcBytesPerElement">The size in bytes per pixel/block element.</param>
	/// <param name="subresource">The destination subresource.</param>
	/// <param name="region">The destination region within the resource.</param>
	/// <param name="isCompressedResource">if set to <c>true</c> the resource is a block/compressed resource</param>
	/// <remarks>
	/// This method is implementing the <a href="http://blogs.msdn.com/b/chuckw/archive/2010/07/28/known-issue-direct3d-11-updatesubresource-and-deferred-contexts.aspx">workaround for deferred context</a>.
	/// </remarks>
	public void UpdateSubresourceSafe(MappedSubresource source, ID3D11Resource resource, int srcBytesPerElement, int subresource, Box region, bool isCompressedResource = false)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		UpdateSubresourceSafe(resource, subresource, region, source.DataPointer, source.RowPitch, source.DepthPitch, srcBytesPerElement, isCompressedResource);
	}

	internal unsafe bool UpdateSubresourceSafe(ID3D11Resource dstResource, int dstSubresource, Box? dstBox, IntPtr pSrcData, int srcRowPitch, int srcDepthPitch, int srcBytesPerElement, bool isCompressedResource)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		if (!_supportsCommandLists.HasValue)
		{
			base.Device.CheckThreadingSupport(out var _, out var supportsCommandLists);
			_supportsCommandLists = supportsCommandLists;
		}
		if (dstBox.HasValue && ContextType == DeviceContextType.Deferred)
		{
			flag = !_supportsCommandLists.Value;
		}
		IntPtr srcData = pSrcData;
		if (flag)
		{
			Box value = dstBox.Value;
			if (isCompressedResource)
			{
				((Box)(ref value))..ctor(value.Left / 4, value.Top / 4, value.Front, value.Right / 4, value.Bottom / 4, value.Back);
			}
			srcData = (IntPtr)((byte*)(void*)pSrcData - value.Front * srcDepthPitch - value.Top * srcRowPitch - value.Left * srcBytesPerElement);
		}
		UpdateSubresource(dstResource, dstSubresource, dstBox, srcData, srcRowPitch, srcDepthPitch);
		return flag;
	}

	public ID3D11DeviceContext(IntPtr nativePtr)
		: base(nativePtr)
	{
	}

	public static explicit operator ID3D11DeviceContext(IntPtr nativePtr)
	{
		if (!(nativePtr == IntPtr.Zero))
		{
			return new ID3D11DeviceContext(nativePtr);
		}
		return null;
	}

	/// <summary>
	///       <para>Sets the constant buffers used by the vertex shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetConstantBuffers</unmanaged-short>
	private unsafe void VSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[7])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the pixel shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetShaderResources</unmanaged-short>
	private unsafe void PSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[8])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Sets a pixel shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetshader" /></para>
	///       <param name="pixelShader">Pointer to a pixel shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11pixelshader">ID3D11PixelShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetShader([In, Optional] ID3D11PixelShader* pPixelShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetShader</unmanaged-short>
	public unsafe void PSSetShader(ID3D11PixelShader pixelShader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((pixelShader != null) ? ((CppObject)pixelShader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[9])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(pixelShader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the pixel shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetsamplers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetSamplers</unmanaged-short>
	private unsafe void PSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[10])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Set a vertex shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetshader" /></para>
	///       <param name="vertexShader">Pointer to a vertex shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11vertexshader">ID3D11VertexShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetShader([In, Optional] ID3D11VertexShader* pVertexShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetShader</unmanaged-short>
	public unsafe void VSSetShader(ID3D11VertexShader vertexShader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((vertexShader != null) ? ((CppObject)vertexShader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[11])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(vertexShader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Draw indexed, non-instanced primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawindexed" /></para>
	///       <param name="indexCount">Number of indices to draw.</param>
	///       <param name="startIndexLocation">The location of the first index read by the GPU from the index buffer.</param>
	///       <param name="baseVertexLocation">A value added to each index before reading a vertex from the vertex buffer.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawIndexed([In] UINT IndexCount, [In] UINT StartIndexLocation, [In] int BaseVertexLocation)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawIndexed</unmanaged-short>
	public unsafe void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, void>)((CppObject)this)[12])(((CppObject)this).NativePointer, indexCount, startIndexLocation, baseVertexLocation);
	}

	/// <summary>
	///       <para>Draw non-indexed, non-instanced primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-draw" /></para>
	///       <param name="vertexCount">Number of vertices to draw.</param>
	///       <param name="startVertexLocation">Index of the first vertex, which is usually an offset in a vertex buffer.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::Draw([In] UINT VertexCount, [In] UINT StartVertexLocation)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Draw</unmanaged-short>
	public unsafe void Draw(int vertexCount, int startVertexLocation)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void>)((CppObject)this)[13])(((CppObject)this).NativePointer, vertexCount, startVertexLocation);
	}

	/// <summary>
	///       <para>Gets a pointer to the data contained in a subresource, and denies the GPU access to that subresource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-map" /></para>
	///       <param name="resource">A pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> interface.</param>
	///       <param name="subresource">Index number of the <a href="https://docs.microsoft.com/windows/desktop/direct3d11/overviews-direct3d-11-resources-subresources">subresource</a>.</param>
	///       <param name="mapType">A <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_map">D3D11_MAP</a>-typed value that specifies the CPU's read and write permissions for a resource.</param>
	///       <param name="mapFlags">
	/// <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_map_flag">Flag</a> that specifies what the CPU does when the GPU is busy. This flag is optional.</param>
	///       <param name="mappedResource">A pointer to the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_mapped_subresource">D3D11_MAPPED_SUBRESOURCE</a> structure for the mapped subresource.
	/// See the Remarks section regarding NULL pointers.</param>
	///     </summary>
	/// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource, [In] UINT Subresource, [In] D3D11_MAP MapType, [In] UINT MapFlags, [Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
	public unsafe Result Map(ID3D11Resource resource, int subresource, MapMode mapType, MapFlags mapFlags, out MappedSubresource mappedResource)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		mappedResource = default(MappedSubresource);
		zero = ((resource != null) ? ((CppObject)resource).NativePointer : IntPtr.Zero);
		Result result;
		fixed (MappedSubresource* ptr = &mappedResource)
		{
			void* ptr2 = ptr;
			result = Result.op_Implicit(((delegate* unmanaged[Stdcall]<IntPtr, void*, int, int, int, void*, int>)((CppObject)this)[14])(((CppObject)this).NativePointer, (void*)zero, subresource, (int)mapType, (int)mapFlags, ptr2));
		}
		GC.KeepAlive(resource);
		return result;
	}

	/// <summary>
	///       <para>Invalidate the pointer to a resource and reenable the GPU's access to that resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-unmap" /></para>
	///       <param name="resource">A pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> interface.</param>
	///       <param name="subresource">A subresource to be unmapped.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::Unmap([In] ID3D11Resource* pResource, [In] UINT Subresource)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Unmap</unmanaged-short>
	public unsafe void Unmap(ID3D11Resource resource, int subresource)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((resource != null) ? ((CppObject)resource).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void>)((CppObject)this)[15])(((CppObject)this).NativePointer, (void*)zero, subresource);
		GC.KeepAlive(resource);
	}

	/// <summary>
	///       <para>Sets the constant buffers used by the pixel shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetConstantBuffers</unmanaged-short>
	private unsafe void PSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[16])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Bind an input-layout object to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iasetinputlayout" /></para>
	///       <param name="inputLayout">A pointer to the input-layout object (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11inputlayout">ID3D11InputLayout</a>), which describes the input buffers that will be read by the IA stage.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IASetInputLayout([In, Optional] ID3D11InputLayout* pInputLayout)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IASetInputLayout</unmanaged-short>
	public unsafe void IASetInputLayout(ID3D11InputLayout inputLayout)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((inputLayout != null) ? ((CppObject)inputLayout).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[17])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(inputLayout);
	}

	/// <summary>
	///       <para>Bind an array of vertex buffers to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iasetvertexbuffers" /></para>
	///       <param name="startSlot">The first input slot for binding. The first vertex buffer is explicitly bound to the start slot; this causes each additional vertex buffer in the array to be implicitly bound to each subsequent input slot. The maximum of 16 or 32 input slots (ranges from 0 to D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT - 1) are available; the <a href="https://docs.microsoft.com/windows/desktop/direct3d11/overviews-direct3d-11-devices-downlevel-intro">maximum number of input slots depends on the feature level</a>.</param>
	///       <param name="numBuffers">The number of vertex buffers in the array. The number of buffers (plus the starting slot) can't exceed the total number of IA-stage input slots (ranges from 0 to D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="vertexBuffers">A pointer to an array of vertex buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>). The vertex buffers must have been created with the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_bind_flag">D3D11_BIND_VERTEX_BUFFER</a> flag.</param>
	///       <param name="strides">Pointer to an array of stride values; one stride value for each buffer in the vertex-buffer array. Each stride is the size (in bytes) of the elements that are to be used from that vertex buffer.</param>
	///       <param name="offsets">Pointer to an array of offset values; one offset value for each buffer in the vertex-buffer array. Each offset is the number of bytes between the first element of a vertex buffer and the first element that will be used.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IASetVertexBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppVertexBuffers, [In, Buffer, Optional] const UINT* pStrides, [In, Buffer, Optional] const UINT* pOffsets)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IASetVertexBuffers</unmanaged-short>
	private unsafe void IASetVertexBuffers(int startSlot, int numBuffers, void* vertexBuffers, void* strides, void* offsets)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void*, void*, void>)((CppObject)this)[18])(((CppObject)this).NativePointer, startSlot, numBuffers, vertexBuffers, strides, offsets);
	}

	/// <summary>
	///       <para>Bind an index buffer to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iasetindexbuffer" /></para>
	///       <param name="indexBuffer">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a> object, that contains indices. The index buffer must have been created with
	/// the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_bind_flag">D3D11_BIND_INDEX_BUFFER</a> flag.</param>
	///       <param name="format">A <a href="https://docs.microsoft.com/windows/desktop/api/dxgiformat/ne-dxgiformat-dxgi_format">DXGI_FORMAT</a> that specifies the format of the data in the index buffer. The only formats allowed for index
	/// buffer data are 16-bit (DXGI_FORMAT_R16_UINT) and 32-bit (DXGI_FORMAT_R32_UINT) integers.</param>
	///       <param name="offset">Offset (in bytes) from the start of the index buffer to the first index to use.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IASetIndexBuffer([In, Optional] ID3D11Buffer* pIndexBuffer, [In] DXGI_FORMAT Format, [In] UINT Offset)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IASetIndexBuffer</unmanaged-short>
	public unsafe void IASetIndexBuffer(ID3D11Buffer indexBuffer, Format format, int offset)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected I4, but got Unknown
		IntPtr zero = IntPtr.Zero;
		zero = ((indexBuffer != null) ? ((CppObject)indexBuffer).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, uint, int, void>)((CppObject)this)[19])(((CppObject)this).NativePointer, (void*)zero, (uint)(int)format, offset);
		GC.KeepAlive(indexBuffer);
	}

	/// <summary>
	///       <para>Draw indexed, instanced primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawindexedinstanced" /></para>
	///       <param name="indexCountPerInstance">Number of indices read from the index buffer for each instance.</param>
	///       <param name="instanceCount">Number of instances to draw.</param>
	///       <param name="startIndexLocation">The location of the first index read by the GPU from the index buffer.</param>
	///       <param name="baseVertexLocation">A value added to each index before reading a vertex from the vertex buffer.</param>
	///       <param name="startInstanceLocation">A value added to each index before reading per-instance data from a vertex buffer.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawIndexedInstanced([In] UINT IndexCountPerInstance, [In] UINT InstanceCount, [In] UINT StartIndexLocation, [In] int BaseVertexLocation, [In] UINT StartInstanceLocation)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawIndexedInstanced</unmanaged-short>
	public unsafe void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, int, int, void>)((CppObject)this)[20])(((CppObject)this).NativePointer, indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
	}

	/// <summary>
	///       <para>Draw non-indexed, instanced primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawinstanced" /></para>
	///       <param name="vertexCountPerInstance">Number of vertices to draw.</param>
	///       <param name="instanceCount">Number of instances to draw.</param>
	///       <param name="startVertexLocation">Index of the first vertex.</param>
	///       <param name="startInstanceLocation">A value added to each index before reading per-instance data from a vertex buffer.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawInstanced([In] UINT VertexCountPerInstance, [In] UINT InstanceCount, [In] UINT StartVertexLocation, [In] UINT StartInstanceLocation)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawInstanced</unmanaged-short>
	public unsafe void DrawInstanced(int vertexCountPerInstance, int instanceCount, int startVertexLocation, int startInstanceLocation)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, int, void>)((CppObject)this)[21])(((CppObject)this).NativePointer, vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
	}

	/// <summary>
	///       <para>Sets the constant buffers used by the geometry shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetConstantBuffers</unmanaged-short>
	private unsafe void GSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[22])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Set a geometry shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetshader" /></para>
	///       <param name="shader">Pointer to a geometry shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11geometryshader">ID3D11GeometryShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetShader([In, Optional] ID3D11GeometryShader* pShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetShader</unmanaged-short>
	public unsafe void GSSetShader(ID3D11GeometryShader shader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((shader != null) ? ((CppObject)shader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[23])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(shader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Bind information about the primitive type, and data order that describes input data for the input assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iasetprimitivetopology" /></para>
	///       <param name="topology">The type of primitive and ordering of the primitive data (see <a href="https://docs.microsoft.com/previous-versions/windows/desktop/legacy/ff476189(v=vs.85)">D3D11_PRIMITIVE_TOPOLOGY</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IASetPrimitiveTopology([In] D3D_PRIMITIVE_TOPOLOGY Topology)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IASetPrimitiveTopology</unmanaged-short>
	public unsafe void IASetPrimitiveTopology(PrimitiveTopology topology)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected I4, but got Unknown
		((delegate* unmanaged[Stdcall]<IntPtr, int, void>)((CppObject)this)[24])(((CppObject)this).NativePointer, (int)topology);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the vertex-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (range is from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources (range is from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetShaderResources</unmanaged-short>
	private unsafe void VSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[25])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the vertex shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetsamplers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetSamplers</unmanaged-short>
	private unsafe void VSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[26])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Mark the beginning of a series of commands.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-begin" /></para>
	///       <param name="async">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11asynchronous">ID3D11Asynchronous</a> interface.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::Begin([In] ID3D11Asynchronous* pAsync)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Begin</unmanaged-short>
	public unsafe void Begin(ID3D11Asynchronous async)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((async != null) ? ((CppObject)async).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[27])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(async);
	}

	/// <summary>
	///       <para>Mark the end of a series of commands.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-end" /></para>
	///       <param name="async">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11asynchronous">ID3D11Asynchronous</a> interface.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::End([In] ID3D11Asynchronous* pAsync)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::End</unmanaged-short>
	public unsafe void End(ID3D11Asynchronous async)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((async != null) ? ((CppObject)async).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[28])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(async);
	}

	/// <summary>
	///       <para>Get data from the graphics processing unit (GPU) asynchronously.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-getdata" /></para>
	///       <param name="async">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11asynchronous">ID3D11Asynchronous</a> interface for the object about which <b>GetData</b> retrieves data.</param>
	///       <param name="data">Address of memory that will receive the data. If <b>NULL</b>, <b>GetData</b> will be used only to check status. The type of data output depends on the type of asynchronous interface.</param>
	///       <param name="dataSize">Size of the data to retrieve or 0. Must be 0 when <i>pData</i> is <b>NULL</b>.</param>
	///       <param name="getDataFlags">Optional flags. Can be 0 or any combination of the flags enumerated by <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_async_getdata_flag">D3D11_ASYNC_GETDATA_FLAG</a>.</param>
	///     </summary>
	/// <unmanaged>HRESULT ID3D11DeviceContext::GetData([In] ID3D11Asynchronous* pAsync, [Out, Buffer, Optional] void* pData, [In] UINT DataSize, [In] UINT GetDataFlags)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetData</unmanaged-short>
	public unsafe Result GetData(ID3D11Asynchronous async, IntPtr data, int dataSize, AsyncGetDataFlags getDataFlags)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		zero = ((async != null) ? ((CppObject)async).NativePointer : IntPtr.Zero);
		Result result = Result.op_Implicit(((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, int, int>)((CppObject)this)[29])(((CppObject)this).NativePointer, (void*)zero, (void*)data, dataSize, (int)getDataFlags));
		GC.KeepAlive(async);
		return result;
	}

	/// <summary>
	///       <para>Set a rendering predicate.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-setpredication" /></para>
	///       <param name="predicate">A pointer to the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11predicate">ID3D11Predicate</a> interface that represents the rendering predicate. A <b>NULL</b> value indicates "no" predication; in this case, the value of <i>PredicateValue</i> is irrelevant but will be preserved for <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-getpredication">ID3D11DeviceContext::GetPredication</a>.</param>
	///       <param name="predicateValue">If <b>TRUE</b>, rendering will be affected by when the predicate's conditions are met. If <b>FALSE</b>, rendering will be affected when the conditions are not met.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::SetPredication([In, Optional] ID3D11Predicate* pPredicate, [In] BOOL PredicateValue)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::SetPredication</unmanaged-short>
	public unsafe void SetPredication(ID3D11Predicate predicate, RawBool predicateValue)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		zero = ((predicate != null) ? ((CppObject)predicate).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, RawBool, void>)((CppObject)this)[30])(((CppObject)this).NativePointer, (void*)zero, predicateValue);
		GC.KeepAlive(predicate);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the geometry shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources(ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetShaderResources</unmanaged-short>
	private unsafe void GSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[31])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the geometry shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetsamplers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetSamplers</unmanaged-short>
	private unsafe void GSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[32])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Bind one or more render targets atomically and the depth-stencil buffer to the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omsetrendertargets" /></para>
	///       <param name="numViews">Number of render targets to bind (ranges between 0 and <b>D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT</b>). If this parameter is nonzero, the number of entries in the array to which <i>ppRenderTargetViews</i> points must equal the number in this parameter.</param>
	///       <param name="renderTargetViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rendertargetview">ID3D11RenderTargetView</a> that represent the render targets to bind to the device. 
	///         If this parameter is <b>NULL</b> and <i>NumViews</i> is 0, no render targets are bound.</param>
	///       <param name="depthStencilView">Pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilview">ID3D11DepthStencilView</a> that represents the depth-stencil view to bind to the device. 
	///         If this parameter is <b>NULL</b>, the depth-stencil view is not bound.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMSetRenderTargets([In] UINT NumViews, [In, Buffer, Optional] const ID3D11RenderTargetView** ppRenderTargetViews, [In, Optional] ID3D11DepthStencilView* pDepthStencilView)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMSetRenderTargets</unmanaged-short>
	private unsafe void OMSetRenderTargets(int numViews, void* renderTargetViews, ID3D11DepthStencilView depthStencilView)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((depthStencilView != null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void*, void>)((CppObject)this)[33])(((CppObject)this).NativePointer, numViews, renderTargetViews, (void*)zero);
		GC.KeepAlive(depthStencilView);
	}

	/// <summary>
	///       <para>Binds resources to the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omsetrendertargetsandunorderedaccessviews" /></para>
	///       <param name="numRTVs">Number of render targets to bind (ranges between 0 and <b>D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT</b>). If this parameter is nonzero, the number of entries in the array to which <i>ppRenderTargetViews</i> points must equal the number in this parameter. If you set <i>NumRTVs</i> to D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL (0xffffffff), this method does not modify the currently bound render-target views (RTVs) and also does not modify depth-stencil view (DSV).</param>
	///       <param name="renderTargetViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rendertargetview">ID3D11RenderTargetView</a>s that represent the render targets to bind to the device.
	/// If this parameter is <b>NULL</b> and <i>NumRTVs</i> is 0, no render targets are bound.</param>
	///       <param name="depthStencilView">Pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilview">ID3D11DepthStencilView</a> that represents the depth-stencil view to bind to the device.
	/// If this parameter is <b>NULL</b>, the depth-stencil view is not bound.</param>
	///       <param name="UAVStartSlot">Index into a zero-based array to begin setting unordered-access views (ranges from 0 to D3D11_PS_CS_UAV_REGISTER_COUNT - 1).
	///
	/// For the Direct3D 11.1 runtime, which is available starting with Windows 8, this value can range from 0 to D3D11_1_UAV_SLOT_COUNT - 1. D3D11_1_UAV_SLOT_COUNT is defined as 64.
	///
	///
	/// For pixel shaders, <i>UAVStartSlot</i> should be equal to the number of render-target views being bound.</param>
	///       <param name="numUAVs">Number of unordered-access views (UAVs) in <i>ppUnorderedAccessViews</i>. If you set <i>NumUAVs</i> to D3D11_KEEP_UNORDERED_ACCESS_VIEWS (0xffffffff), this method does not modify the currently bound unordered-access views.
	///
	///
	/// For the Direct3D 11.1 runtime, which is available starting with Windows 8, this value can range from 0 to D3D11_1_UAV_SLOT_COUNT - <i>UAVStartSlot</i>.</param>
	///       <param name="unorderedAccessViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a>s that represent the unordered-access views to bind to the device.
	/// If this parameter is <b>NULL</b> and <i>NumUAVs</i> is 0, no unordered-access views are bound.</param>
	///       <param name="uAVInitialCounts">An array of append and consume buffer offsets. A value of -1 indicates to keep the current offset. Any other values set the hidden counter
	/// for that appendable and consumable UAV. <i>pUAVInitialCounts</i> is  relevant only for UAVs that were created with either
	/// <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_buffer_uav_flag">D3D11_BUFFER_UAV_FLAG_APPEND</a> or <b>D3D11_BUFFER_UAV_FLAG_COUNTER</b> specified
	/// when the UAV was created; otherwise, the argument is ignored.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMSetRenderTargetsAndUnorderedAccessViews([In] UINT NumRTVs, [In, Buffer, Optional] const ID3D11RenderTargetView** ppRenderTargetViews, [In, Optional] ID3D11DepthStencilView* pDepthStencilView, [In] UINT UAVStartSlot, [In] UINT NumUAVs, [In, Buffer, Optional] const ID3D11UnorderedAccessView** ppUnorderedAccessViews, [In, Buffer, Optional] const UINT* pUAVInitialCounts)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMSetRenderTargetsAndUnorderedAccessViews</unmanaged-short>
	private unsafe void OMSetRenderTargetsAndUnorderedAccessViews(int numRTVs, void* renderTargetViews, IntPtr depthStencilView, int uAVStartSlot, int numUAVs, void* unorderedAccessViews, void* unorderedAccessViewInitialCounts)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void*, int, int, void*, void*, void>)((CppObject)this)[34])(((CppObject)this).NativePointer, numRTVs, renderTargetViews, (void*)depthStencilView, uAVStartSlot, numUAVs, unorderedAccessViews, unorderedAccessViewInitialCounts);
	}

	/// <summary>
	///       <para>Set the blend state of the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omsetblendstate" /></para>
	///       <param name="blendState">Pointer to a blend-state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11blendstate">ID3D11BlendState</a>). Pass <b>NULL</b> for a default blend state. For more info about default blend state, see Remarks.</param>
	///       <param name="blendFactor">Array of blend factors, one for each RGBA component. The blend factors modulate values for the pixel shader, render target, or both. If you created  the blend-state object with <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_blend">D3D11_BLEND_BLEND_FACTOR</a> or <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_blend">D3D11_BLEND_INV_BLEND_FACTOR</a>, the blending stage uses the non-NULL array of blend factors. If you didn't create the blend-state object with <b>D3D11_BLEND_BLEND_FACTOR</b> or <b>D3D11_BLEND_INV_BLEND_FACTOR</b>, the blending stage does not use the non-NULL array of blend factors; the runtime stores the blend factors, and you can later call <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-omgetblendstate">ID3D11DeviceContext::OMGetBlendState</a> to retrieve the blend factors. If you pass <b>NULL</b>, the runtime uses or stores a blend factor equal to { 1, 1, 1, 1 }.</param>
	///       <param name="sampleMask">32-bit sample coverage. The default value is 0xffffffff. See remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMSetBlendState([In, Optional] ID3D11BlendState* pBlendState, [In, Optional] const float* BlendFactor, [In] UINT SampleMask)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMSetBlendState</unmanaged-short>
	private unsafe void OMSetBlendState(ID3D11BlendState blendState, float? blendFactor, int sampleMask)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((blendState != null) ? ((CppObject)blendState).NativePointer : IntPtr.Zero);
		float value = default(float);
		if (blendFactor.HasValue)
		{
			value = blendFactor.Value;
		}
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[OMSetBlendState__vtbl_index])(((CppObject)this).NativePointer, (void*)zero, (!blendFactor.HasValue) ? null : (&value), sampleMask);
		GC.KeepAlive(blendState);
	}

	/// <summary>
	///       <para>Sets the depth-stencil state of the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omsetdepthstencilstate" /></para>
	///       <param name="depthStencilState">Pointer to a depth-stencil state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilstate">ID3D11DepthStencilState</a>) to bind to the device. Set this to <b>NULL</b> to use the default state listed in <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_depth_stencil_desc">D3D11_DEPTH_STENCIL_DESC</a>.</param>
	///       <param name="stencilRef">Reference value to perform against when doing a depth-stencil test. See remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMSetDepthStencilState([In, Optional] ID3D11DepthStencilState* pDepthStencilState, [In] UINT StencilRef)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMSetDepthStencilState</unmanaged-short>
	public unsafe void OMSetDepthStencilState(ID3D11DepthStencilState depthStencilState, int stencilRef = 0)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((depthStencilState != null) ? ((CppObject)depthStencilState).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void>)((CppObject)this)[36])(((CppObject)this).NativePointer, (void*)zero, stencilRef);
		GC.KeepAlive(depthStencilState);
	}

	/// <summary>
	///       <para>Set the target output buffers for the stream-output stage of the pipeline.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-sosettargets" /></para>
	///       <param name="numBuffers">The number of buffer to bind to the device. A maximum of four output buffers can be set. If less than four are defined by the call, the remaining buffer slots are set to <b>NULL</b>. See Remarks.</param>
	///       <param name="sOTargets">The array of output buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to bind to the device. The buffers must have been created with the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_bind_flag">D3D11_BIND_STREAM_OUTPUT</a> flag.</param>
	///       <param name="offsets">Array of offsets to the output buffers from <i>ppSOTargets</i>, one offset for each buffer. The offset values must be in bytes.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::SOSetTargets([In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppSOTargets, [In, Buffer, Optional] const UINT* pOffsets)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::SOSetTargets</unmanaged-short>
	private unsafe void SOSetTargets(int numBuffers, void* sOTargets, void* offsets)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void*, void>)((CppObject)this)[37])(((CppObject)this).NativePointer, numBuffers, sOTargets, offsets);
	}

	/// <summary>
	///       <para>Draw geometry of an unknown size.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawauto" /></para>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawAuto()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawAuto</unmanaged-short>
	public unsafe void DrawAuto()
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void>)((CppObject)this)[38])(((CppObject)this).NativePointer);
	}

	/// <summary>
	///       <para>Draw indexed, instanced, GPU-generated primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawindexedinstancedindirect" /></para>
	///       <param name="bufferForArgs">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>, which is a buffer containing the GPU generated primitives.</param>
	///       <param name="alignedByteOffsetForArgs">Offset in <i>pBufferForArgs</i> to the start of the GPU generated primitives.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawIndexedInstancedIndirect([In] ID3D11Buffer* pBufferForArgs, [In] UINT AlignedByteOffsetForArgs)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawIndexedInstancedIndirect</unmanaged-short>
	public unsafe void DrawIndexedInstancedIndirect(ID3D11Buffer bufferForArgs, int alignedByteOffsetForArgs)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((bufferForArgs != null) ? ((CppObject)bufferForArgs).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void>)((CppObject)this)[39])(((CppObject)this).NativePointer, (void*)zero, alignedByteOffsetForArgs);
		GC.KeepAlive(bufferForArgs);
	}

	/// <summary>
	///       <para>Draw instanced, GPU-generated primitives.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-drawinstancedindirect" /></para>
	///       <param name="bufferForArgs">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>, which is a buffer containing the GPU generated primitives.</param>
	///       <param name="alignedByteOffsetForArgs">Offset in <i>pBufferForArgs</i> to the start of the GPU generated primitives.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DrawInstancedIndirect([In] ID3D11Buffer* pBufferForArgs, [In] UINT AlignedByteOffsetForArgs)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DrawInstancedIndirect</unmanaged-short>
	public unsafe void DrawInstancedIndirect(ID3D11Buffer bufferForArgs, int alignedByteOffsetForArgs)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((bufferForArgs != null) ? ((CppObject)bufferForArgs).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void>)((CppObject)this)[40])(((CppObject)this).NativePointer, (void*)zero, alignedByteOffsetForArgs);
		GC.KeepAlive(bufferForArgs);
	}

	/// <summary>
	///       <para>Execute a command list from a thread group.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dispatch" /></para>
	///       <param name="threadGroupCountX">The number of groups dispatched in the x direction. <i>ThreadGroupCountX</i> must be less than or equal to D3D11_CS_DISPATCH_MAX_THREAD_GROUPS_PER_DIMENSION (65535).</param>
	///       <param name="threadGroupCountY">The number of groups dispatched in the y direction. <i>ThreadGroupCountY</i> must be less than or equal to D3D11_CS_DISPATCH_MAX_THREAD_GROUPS_PER_DIMENSION (65535).</param>
	///       <param name="threadGroupCountZ">The number of groups dispatched in the z direction.  <i>ThreadGroupCountZ</i> must be less than or equal to D3D11_CS_DISPATCH_MAX_THREAD_GROUPS_PER_DIMENSION (65535). 
	///         In feature level 10 the value for <i>ThreadGroupCountZ</i> must be 1.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::Dispatch([In] UINT ThreadGroupCountX, [In] UINT ThreadGroupCountY, [In] UINT ThreadGroupCountZ)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Dispatch</unmanaged-short>
	public unsafe void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, void>)((CppObject)this)[41])(((CppObject)this).NativePointer, threadGroupCountX, threadGroupCountY, threadGroupCountZ);
	}

	/// <summary>
	///       <para>Execute a command list over one or more thread groups.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dispatchindirect" /></para>
	///       <param name="bufferForArgs">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>, which must be loaded with data that matches the argument list for <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-dispatch">ID3D11DeviceContext::Dispatch</a>.</param>
	///       <param name="alignedByteOffsetForArgs">A byte-aligned offset between the start of the buffer and the arguments.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DispatchIndirect([In] ID3D11Buffer* pBufferForArgs, [In] UINT AlignedByteOffsetForArgs)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DispatchIndirect</unmanaged-short>
	public unsafe void DispatchIndirect(ID3D11Buffer bufferForArgs, int alignedByteOffsetForArgs)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((bufferForArgs != null) ? ((CppObject)bufferForArgs).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void>)((CppObject)this)[42])(((CppObject)this).NativePointer, (void*)zero, alignedByteOffsetForArgs);
		GC.KeepAlive(bufferForArgs);
	}

	/// <summary>
	///       <para>Set the rasterizer state for the rasterizer stage of the pipeline.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rssetstate" /></para>
	///       <param name="rasterizerState">Pointer to a rasterizer-state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rasterizerstate">ID3D11RasterizerState</a>) to bind to the pipeline.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSSetState([In, Optional] ID3D11RasterizerState* pRasterizerState)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSSetState</unmanaged-short>
	public unsafe void RSSetState(ID3D11RasterizerState rasterizerState)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((rasterizerState != null) ? ((CppObject)rasterizerState).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[43])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(rasterizerState);
	}

	/// <summary>
	///       <para>Bind an array of viewports to the rasterizer stage of the pipeline.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rssetviewports" /></para>
	///       <param name="numViewports">Number of viewports to bind.</param>
	///       <param name="viewports">An array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_viewport">D3D11_VIEWPORT</a> structures to bind to the device. See the structure page for details about how the viewport size is dependent on the device feature level which has changed between Direct3D 11 and Direct3D 10.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSSetViewports([In] UINT NumViewports, [In, Buffer, Optional] const D3D11_VIEWPORT* pViewports)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSSetViewports</unmanaged-short>
	internal unsafe void RSSetViewports(int numViewports, void* viewports)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void>)((CppObject)this)[44])(((CppObject)this).NativePointer, numViewports, viewports);
	}

	/// <summary>
	///       <para>Bind an array of scissor rectangles to the rasterizer stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rssetscissorrects" /></para>
	///       <param name="numRects">Number of scissor rectangles to bind.</param>
	///       <param name="rects">An array of scissor rectangles (see <a href="https://docs.microsoft.com/windows/desktop/direct3d11/d3d11-rect">D3D11_RECT</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSSetScissorRects([In] UINT NumRects, [In, Buffer, Optional] const RECT* pRects)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSSetScissorRects</unmanaged-short>
	internal unsafe void RSSetScissorRects(int numRects, void* rects)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void>)((CppObject)this)[45])(((CppObject)this).NativePointer, numRects, rects);
	}

	/// <summary>
	///       <para>Copy a region from a source resource to a destination resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-copysubresourceregion" /></para>
	///       <param name="dstResource">A pointer to the destination resource (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a>).</param>
	///       <param name="dstSubresource">Destination subresource index.</param>
	///       <param name="dstX">The x-coordinate of the upper left corner of the destination region.</param>
	///       <param name="dstY">The y-coordinate of the upper left corner of the destination region. For a 1D subresource, this must be zero.</param>
	///       <param name="dstZ">The z-coordinate of the upper left corner of the destination region. For a 1D or 2D subresource, this must be zero.</param>
	///       <param name="srcResource">A pointer to the source resource (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a>).</param>
	///       <param name="srcSubresource">Source subresource index.</param>
	///       <param name="srcBox">A pointer to a 3D box (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_box">D3D11_BOX</a>) that defines the source subresource that can be copied. If <b>NULL</b>, the entire source subresource is copied. The box must fit within the source resource.
	///
	/// An empty box results in a no-op. A box is empty if the top value is greater than or equal to the bottom value, or the left value is greater than or equal to the right value, or the front value is greater than or equal to the back value. When the box is empty, <b>CopySubresourceRegion</b> doesn't perform a copy operation.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CopySubresourceRegion([In] ID3D11Resource* pDstResource, [In] UINT DstSubresource, [In] UINT DstX, [In] UINT DstY, [In] UINT DstZ, [In] ID3D11Resource* pSrcResource, [In] UINT SrcSubresource, [In, Optional] const D3D11_BOX* pSrcBox)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CopySubresourceRegion</unmanaged-short>
	public unsafe void CopySubresourceRegion(ID3D11Resource dstResource, int dstSubresource, int dstX, int dstY, int dstZ, ID3D11Resource srcResource, int srcSubresource, Box? srcBox = null)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		IntPtr zero2 = IntPtr.Zero;
		zero = ((dstResource != null) ? ((CppObject)dstResource).NativePointer : IntPtr.Zero);
		zero2 = ((srcResource != null) ? ((CppObject)srcResource).NativePointer : IntPtr.Zero);
		Box value = default(Box);
		if (srcBox.HasValue)
		{
			value = srcBox.Value;
		}
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, int, int, int, void*, int, void*, void>)((CppObject)this)[46])(((CppObject)this).NativePointer, (void*)zero, dstSubresource, dstX, dstY, dstZ, (void*)zero2, srcSubresource, (!srcBox.HasValue) ? null : (&value));
		GC.KeepAlive(dstResource);
		GC.KeepAlive(srcResource);
	}

	/// <summary>
	///       <para>Copy the entire contents of the source resource to the destination resource using the GPU.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-copyresource" /></para>
	///       <param name="dstResource">A pointer to the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> interface that represents the destination resource.</param>
	///       <param name="srcResource">A pointer to the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> interface that represents the source resource.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CopyResource([In] ID3D11Resource* pDstResource, [In] ID3D11Resource* pSrcResource)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CopyResource</unmanaged-short>
	public unsafe void CopyResource(ID3D11Resource dstResource, ID3D11Resource srcResource)
	{
		IntPtr zero = IntPtr.Zero;
		IntPtr zero2 = IntPtr.Zero;
		zero = ((dstResource != null) ? ((CppObject)dstResource).NativePointer : IntPtr.Zero);
		zero2 = ((srcResource != null) ? ((CppObject)srcResource).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[47])(((CppObject)this).NativePointer, (void*)zero, (void*)zero2);
		GC.KeepAlive(dstResource);
		GC.KeepAlive(srcResource);
	}

	/// <summary>
	///       <para>The CPU copies data from memory to a subresource created in non-mappable memory.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-updatesubresource" /></para>
	///       <param name="dstResource">A pointer to the destination resource (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a>).</param>
	///       <param name="dstSubresource">A zero-based index, that identifies the destination subresource. See <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-d3d11calcsubresource">D3D11CalcSubresource</a> for more details.</param>
	///       <param name="dstBox">A pointer to a box that defines the portion of the destination subresource to copy the resource data into. Coordinates are in bytes for buffers and in texels for textures. If <b>NULL</b>, the data is written to the destination subresource with no offset. The dimensions of the source must fit the destination (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_box">D3D11_BOX</a>).
	///
	/// An empty box results in a no-op. A box is empty if the top value is greater than or equal to the bottom value, or the left value is greater than or equal to the right value, or the front value is greater than or equal to the back value. When the box is empty, <b>UpdateSubresource</b> doesn't perform an update operation.</param>
	///       <param name="srcData">A pointer to the source data in memory.</param>
	///       <param name="srcRowPitch">The size of one row of the source data.</param>
	///       <param name="srcDepthPitch">The size of one depth slice of source data.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::UpdateSubresource([In] ID3D11Resource* pDstResource, [In] UINT DstSubresource, [In, Optional] const D3D11_BOX* pDstBox, [In] const void* pSrcData, [In] UINT SrcRowPitch, [In] UINT SrcDepthPitch)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::UpdateSubresource</unmanaged-short>
	public unsafe void UpdateSubresource(ID3D11Resource dstResource, int dstSubresource, Box? dstBox, IntPtr srcData, int srcRowPitch, int srcDepthPitch)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		zero = ((dstResource != null) ? ((CppObject)dstResource).NativePointer : IntPtr.Zero);
		Box value = default(Box);
		if (dstBox.HasValue)
		{
			value = dstBox.Value;
		}
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void*, void*, int, int, void>)((CppObject)this)[48])(((CppObject)this).NativePointer, (void*)zero, dstSubresource, (!dstBox.HasValue) ? null : (&value), (void*)srcData, srcRowPitch, srcDepthPitch);
		GC.KeepAlive(dstResource);
	}

	/// <summary>
	///       <para>Copies data from a buffer holding variable length data.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-copystructurecount" /></para>
	///       <param name="dstBuffer">Pointer to <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>.  This can be any buffer resource that other copy commands, 
	///         such as <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-copyresource">ID3D11DeviceContext::CopyResource</a> or <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-copysubresourceregion">ID3D11DeviceContext::CopySubresourceRegion</a>, are able to write to.</param>
	///       <param name="dstAlignedByteOffset">Offset from the start of <i>pDstBuffer</i> to write 32-bit UINT structure (vertex) count from <i>pSrcView</i>.</param>
	///       <param name="srcView">Pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a> of a Structured Buffer resource created with either 
	///         <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_buffer_uav_flag">D3D11_BUFFER_UAV_FLAG_APPEND</a> or <b>D3D11_BUFFER_UAV_FLAG_COUNTER</b> specified 
	///         when the UAV was created.   These types of resources have hidden counters tracking "how many" records have 
	///         been written.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CopyStructureCount([In] ID3D11Buffer* pDstBuffer, [In] UINT DstAlignedByteOffset, [In] ID3D11UnorderedAccessView* pSrcView)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CopyStructureCount</unmanaged-short>
	public unsafe void CopyStructureCount(ID3D11Buffer dstBuffer, int dstAlignedByteOffset, ID3D11UnorderedAccessView srcView)
	{
		IntPtr zero = IntPtr.Zero;
		IntPtr zero2 = IntPtr.Zero;
		zero = ((dstBuffer != null) ? ((CppObject)dstBuffer).NativePointer : IntPtr.Zero);
		zero2 = ((srcView != null) ? ((CppObject)srcView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void*, void>)((CppObject)this)[49])(((CppObject)this).NativePointer, (void*)zero, dstAlignedByteOffset, (void*)zero2);
		GC.KeepAlive(dstBuffer);
		GC.KeepAlive(srcView);
	}

	/// <summary>
	///       <para>Set all the elements in a render target to one value.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-clearrendertargetview" /></para>
	///       <param name="renderTargetView">Pointer to the render target.</param>
	///       <param name="colorRGBA">A 4-component array that represents the color to fill the render target with.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ClearRenderTargetView([In] ID3D11RenderTargetView* pRenderTargetView, [In] const VORTICE_COLOR4* ColorRGBA[0])</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ClearRenderTargetView</unmanaged-short>
	public unsafe void ClearRenderTargetView(ID3D11RenderTargetView renderTargetView, Color4 colorRGBA)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((renderTargetView != null) ? ((CppObject)renderTargetView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[50])(((CppObject)this).NativePointer, (void*)zero, &colorRGBA);
		GC.KeepAlive(renderTargetView);
	}

	/// <summary>
	///       <para>Clears an unordered access resource with bit-precise values.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-clearunorderedaccessviewuint" /></para>
	///       <param name="unorderedAccessView">The <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a> to clear.</param>
	///       <param name="values">Values to copy to corresponding channels, see remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ClearUnorderedAccessViewUint([In] ID3D11UnorderedAccessView* pUnorderedAccessView, [In] const VORTICE_INT4* Values[0])</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ClearUnorderedAccessViewUint</unmanaged-short>
	public unsafe void ClearUnorderedAccessView(ID3D11UnorderedAccessView unorderedAccessView, Int4 values)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((unorderedAccessView != null) ? ((CppObject)unorderedAccessView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[51])(((CppObject)this).NativePointer, (void*)zero, &values);
		GC.KeepAlive(unorderedAccessView);
	}

	/// <summary>
	///       <para>Clears an unordered access resource with a float value.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-clearunorderedaccessviewfloat" /></para>
	///       <param name="unorderedAccessView">The <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a> to clear.</param>
	///       <param name="values">Values to copy to corresponding channels, see remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ClearUnorderedAccessViewFloat([In] ID3D11UnorderedAccessView* pUnorderedAccessView, [In] const VORTICE_VECTOR4* Values[0])</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ClearUnorderedAccessViewFloat</unmanaged-short>
	public unsafe void ClearUnorderedAccessView(ID3D11UnorderedAccessView unorderedAccessView, Vector4 values)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((unorderedAccessView != null) ? ((CppObject)unorderedAccessView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[52])(((CppObject)this).NativePointer, (void*)zero, &values);
		GC.KeepAlive(unorderedAccessView);
	}

	/// <summary>
	///       <para>Clears the depth-stencil resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cleardepthstencilview" /></para>
	///       <param name="depthStencilView">Pointer to the depth stencil to be cleared.</param>
	///       <param name="clearFlags">Identify the type of data to clear (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_clear_flag">D3D11_CLEAR_FLAG</a>).</param>
	///       <param name="depth">Clear the depth buffer with this value. This value will be clamped between 0 and 1.</param>
	///       <param name="stencil">Clear the stencil buffer with this value.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ClearDepthStencilView([In] ID3D11DepthStencilView* pDepthStencilView, [In] UINT ClearFlags, [In] float Depth, [In] unsigned char Stencil)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ClearDepthStencilView</unmanaged-short>
	public unsafe void ClearDepthStencilView(ID3D11DepthStencilView depthStencilView, DepthStencilClearFlags clearFlags, float depth, byte stencil)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((depthStencilView != null) ? ((CppObject)depthStencilView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, float, byte, void>)((CppObject)this)[53])(((CppObject)this).NativePointer, (void*)zero, (int)clearFlags, depth, stencil);
		GC.KeepAlive(depthStencilView);
	}

	/// <summary>
	///       <para>Generates mipmaps for the given shader resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-generatemips" /></para>
	///       <param name="shaderResourceView">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">ID3D11ShaderResourceView</a> interface that represents the shader resource.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GenerateMips([In] ID3D11ShaderResourceView* pShaderResourceView)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GenerateMips</unmanaged-short>
	public unsafe void GenerateMips(ID3D11ShaderResourceView shaderResourceView)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((shaderResourceView != null) ? ((CppObject)shaderResourceView).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[54])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(shaderResourceView);
	}

	/// <summary>
	///       <para>Sets the minimum level-of-detail (LOD) for a resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-setresourceminlod" /></para>
	///       <param name="resource">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> that represents the resource.</param>
	///       <param name="minLOD">The level-of-detail, which ranges between 0 and the maximum number of mipmap levels of the resource. For example, the maximum number of mipmap levels of a 1D texture is specified in the  <b>MipLevels</b> member of the  <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_texture1d_desc">D3D11_TEXTURE1D_DESC</a> structure.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::SetResourceMinLOD([In] ID3D11Resource* pResource, [In] float MinLOD)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::SetResourceMinLOD</unmanaged-short>
	public unsafe void SetResourceMinLOD(ID3D11Resource resource, float minLOD)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((resource != null) ? ((CppObject)resource).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, float, void>)((CppObject)this)[55])(((CppObject)this).NativePointer, (void*)zero, minLOD);
		GC.KeepAlive(resource);
	}

	/// <summary>
	///       <para>Gets the minimum level-of-detail (LOD).</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-getresourceminlod" /></para>
	///       <param name="resource">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a> which represents the resource.</param>
	///     </summary>
	/// <unmanaged>float ID3D11DeviceContext::GetResourceMinLOD([In] ID3D11Resource* pResource)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetResourceMinLOD</unmanaged-short>
	public unsafe float GetResourceMinLOD(ID3D11Resource resource)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((resource != null) ? ((CppObject)resource).NativePointer : IntPtr.Zero);
		float result = ((delegate* unmanaged[Stdcall]<IntPtr, void*, float>)((CppObject)this)[56])(((CppObject)this).NativePointer, (void*)zero);
		GC.KeepAlive(resource);
		return result;
	}

	/// <summary>
	///       <para>Copy a multisampled resource into a non-multisampled resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-resolvesubresource" /></para>
	///       <param name="dstResource">Destination resource. Must be a created with the <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_usage">D3D11_USAGE_DEFAULT</a> flag and be single-sampled. See <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11resource">ID3D11Resource</a>.</param>
	///       <param name="dstSubresource">A zero-based index, that identifies the destination subresource. Use <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-d3d11calcsubresource">D3D11CalcSubresource</a> to calculate the index.</param>
	///       <param name="srcResource">Source resource. Must be multisampled.</param>
	///       <param name="srcSubresource">The source subresource of the source resource.</param>
	///       <param name="format">A <a href="https://docs.microsoft.com/windows/desktop/api/dxgiformat/ne-dxgiformat-dxgi_format">DXGI_FORMAT</a> that indicates how the multisampled resource will be resolved to a single-sampled resource. 
	///       See remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ResolveSubresource([In] ID3D11Resource* pDstResource, [In] UINT DstSubresource, [In] ID3D11Resource* pSrcResource, [In] UINT SrcSubresource, [In] DXGI_FORMAT Format)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ResolveSubresource</unmanaged-short>
	public unsafe void ResolveSubresource(ID3D11Resource dstResource, int dstSubresource, ID3D11Resource srcResource, int srcSubresource, Format format)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected I4, but got Unknown
		IntPtr zero = IntPtr.Zero;
		IntPtr zero2 = IntPtr.Zero;
		zero = ((dstResource != null) ? ((CppObject)dstResource).NativePointer : IntPtr.Zero);
		zero2 = ((srcResource != null) ? ((CppObject)srcResource).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, int, void*, int, uint, void>)((CppObject)this)[57])(((CppObject)this).NativePointer, (void*)zero, dstSubresource, (void*)zero2, srcSubresource, (uint)(int)format);
		GC.KeepAlive(dstResource);
		GC.KeepAlive(srcResource);
	}

	/// <summary>
	///       <para>Queues commands from a command list onto a device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-executecommandlist" /></para>
	///       <param name="commandList">A pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11commandlist">ID3D11CommandList</a> interface that encapsulates a command list.</param>
	///       <param name="restoreContextState">A Boolean flag that determines whether the target context state is saved prior to and restored after the execution of a command list. Use <b>TRUE</b> to indicate that the runtime needs to save and restore the state. Use <b>FALSE</b> to indicate that no state shall be saved or restored, which causes the target context to  return to its default state after the command list executes. Applications should typically use <b>FALSE</b> unless they will restore the state to be nearly equivalent to the state that the runtime would restore if <b>TRUE</b> were passed. When applications use <b>FALSE</b>, they can avoid unnecessary and inefficient state transitions.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ExecuteCommandList([In] ID3D11CommandList* pCommandList, [In] BOOL RestoreContextState)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ExecuteCommandList</unmanaged-short>
	public unsafe void ExecuteCommandList(ID3D11CommandList commandList, RawBool restoreContextState)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		zero = ((commandList != null) ? ((CppObject)commandList).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, RawBool, void>)((CppObject)this)[58])(((CppObject)this).NativePointer, (void*)zero, restoreContextState);
		GC.KeepAlive(commandList);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the hull-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources(ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetShaderResources</unmanaged-short>
	private unsafe void HSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[59])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Set a hull shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetshader" /></para>
	///       <param name="hullShader">Pointer to a hull shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11hullshader">ID3D11HullShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetShader([In, Optional] ID3D11HullShader* pHullShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetShader</unmanaged-short>
	public unsafe void HSSetShader(ID3D11HullShader hullShader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((hullShader != null) ? ((CppObject)hullShader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[60])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(hullShader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the hull-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetsamplers" /></para>
	///       <param name="startSlot">Index into the zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetSamplers</unmanaged-short>
	private unsafe void HSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[61])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Set the constant buffers used by the hull-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetConstantBuffers</unmanaged-short>
	private unsafe void HSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[62])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the domain-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources(ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetShaderResources</unmanaged-short>
	private unsafe void DSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[63])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Set a domain shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetshader" /></para>
	///       <param name="domainShader">Pointer to a domain shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11domainshader">ID3D11DomainShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetShader([In, Optional] ID3D11DomainShader* pDomainShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetShader</unmanaged-short>
	public unsafe void DSSetShader(ID3D11DomainShader domainShader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((domainShader != null) ? ((CppObject)domainShader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[64])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(domainShader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the domain-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetsamplers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetSamplers</unmanaged-short>
	private unsafe void DSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[65])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Sets the constant buffers used by the domain-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetConstantBuffers</unmanaged-short>
	private unsafe void DSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[66])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Bind an array of shader resources to the compute-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting shader resources to (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">Number of shader resources to set. Up to a maximum of 128 slots are available for shader resources(ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to set to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [In, Buffer, Optional] const ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetShaderResources</unmanaged-short>
	private unsafe void CSSetShaderResources(int startSlot, int numViews, void* shaderResourceViews)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[67])(((CppObject)this).NativePointer, startSlot, numViews, shaderResourceViews);
	}

	/// <summary>
	///       <para>Sets an array of views for an unordered resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetunorderedaccessviews" /></para>
	///       <param name="startSlot">Index of the first element in the zero-based array to begin setting  (ranges from 0 to D3D11_1_UAV_SLOT_COUNT - 1). D3D11_1_UAV_SLOT_COUNT is defined as 64.</param>
	///       <param name="numUAVs">Number of views to set (ranges from 0 to D3D11_1_UAV_SLOT_COUNT - <i>StartSlot</i>).</param>
	///       <param name="unorderedAccessViews">A pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a> pointers to be set by the method.</param>
	///       <param name="uAVInitialCounts">An array of append and consume buffer offsets. A value of -1 indicates to keep the current offset. Any other values set the hidden counter
	/// for that appendable and consumable UAV. <i>pUAVInitialCounts</i> is only relevant for UAVs that were created with either
	/// <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ne-d3d11-d3d11_buffer_uav_flag">D3D11_BUFFER_UAV_FLAG_APPEND</a> or <b>D3D11_BUFFER_UAV_FLAG_COUNTER</b> specified
	/// when the UAV was created; otherwise, the argument is ignored.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetUnorderedAccessViews([In] UINT StartSlot, [In] UINT NumUAVs, [In, Buffer, Optional] const ID3D11UnorderedAccessView** ppUnorderedAccessViews, [In, Buffer, Optional] const UINT* pUAVInitialCounts)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetUnorderedAccessViews</unmanaged-short>
	private unsafe void CSSetUnorderedAccessViews(int startSlot, int numUAVs, void* unorderedAccessViews, void* unorderedAccessViewInitialCounts)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void*, void>)((CppObject)this)[68])(((CppObject)this).NativePointer, startSlot, numUAVs, unorderedAccessViews, unorderedAccessViewInitialCounts);
	}

	/// <summary>
	///       <para>Set a compute shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetshader" /></para>
	///       <param name="computeShader">Pointer to a compute shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11computeshader">ID3D11ComputeShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetShader([In, Optional] ID3D11ComputeShader* pComputeShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetShader</unmanaged-short>
	public unsafe void CSSetShader(ID3D11ComputeShader computeShader, ID3D11ClassInstance[] classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		zero = ((computeShader != null) ? ((CppObject)computeShader).NativePointer : IntPtr.Zero);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				ref IntPtr reference = ref span[i];
				ID3D11ClassInstance obj = classInstances[i];
				reference = ((obj != null) ? ((CppObject)obj).NativePointer : IntPtr.Zero);
			}
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[69])(((CppObject)this).NativePointer, (void*)zero, ptr2, numClassInstances);
		}
		GC.KeepAlive(computeShader);
		GC.KeepAlive(classInstances);
	}

	/// <summary>
	///       <para>Set an array of sampler states to the compute-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetsamplers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin setting samplers to (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers in the array. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>). See Remarks.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [In, Buffer, Optional] const ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetSamplers</unmanaged-short>
	private unsafe void CSSetSamplers(int startSlot, int numSamplers, void* samplers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[70])(((CppObject)this).NativePointer, startSlot, numSamplers, samplers);
	}

	/// <summary>
	///       <para>Sets the constant buffers used by the compute-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the zero-based array to begin setting constant buffers to (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - 1).</param>
	///       <param name="numBuffers">Number of buffers to set (ranges from 0 to <b>D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT</b> - <i>StartSlot</i>).</param>
	///       <param name="constantBuffers">Array of constant buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) being given to the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [In, Buffer, Optional] const ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetConstantBuffers</unmanaged-short>
	private unsafe void CSSetConstantBuffers(int startSlot, int numBuffers, void* constantBuffers)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[71])(((CppObject)this).NativePointer, startSlot, numBuffers, constantBuffers);
	}

	/// <summary>
	///       <para>Get the constant buffers used by the vertex shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vsgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSGetConstantBuffers</unmanaged-short>
	public unsafe void VSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[72])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the pixel shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-psgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSGetShaderResources</unmanaged-short>
	public unsafe void PSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[73])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the pixel shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-psgetshader" /></para>
	///       <param name="pixelShader">Address of a pointer to a pixel shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11pixelshader">ID3D11PixelShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSGetShader([Out, Optional] ID3D11PixelShader** ppPixelShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSGetShader</unmanaged-short>
	public unsafe void PSGetShader(out ID3D11PixelShader pixelShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[74])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		pixelShader = ((zero != IntPtr.Zero) ? new ID3D11PixelShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler states from the pixel shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-psgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Arry of sampler-state interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>) to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSGetSamplers</unmanaged-short>
	public unsafe void PSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[75])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the vertex shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vsgetshader" /></para>
	///       <param name="vertexShader">Address of a pointer to a vertex shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11vertexshader">ID3D11VertexShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSGetShader([Out, Optional] ID3D11VertexShader** ppVertexShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSGetShader</unmanaged-short>
	public unsafe void VSGetShader(out ID3D11VertexShader vertexShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[76])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		vertexShader = ((zero != IntPtr.Zero) ? new ID3D11VertexShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the constant buffers used by the pixel shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-psgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSGetConstantBuffers</unmanaged-short>
	public unsafe void PSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[77])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get a pointer to the input-layout object that is bound to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iagetinputlayout" /></para>
	///       <param name="inputLayout">A pointer to the input-layout object (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11inputlayout">ID3D11InputLayout</a>), which describes the input buffers that will be read by the IA stage.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IAGetInputLayout([Out, Optional] ID3D11InputLayout** ppInputLayout)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IAGetInputLayout</unmanaged-short>
	public unsafe ID3D11InputLayout IAGetInputLayout()
	{
		IntPtr zero = IntPtr.Zero;
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[78])(((CppObject)this).NativePointer, &zero);
		if (!(zero != IntPtr.Zero))
		{
			return null;
		}
		return new ID3D11InputLayout(zero);
	}

	/// <summary>
	///       <para>Get the vertex buffers bound to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iagetvertexbuffers" /></para>
	///       <param name="startSlot">The input slot of the first vertex buffer to get. The first vertex buffer is explicitly bound to the start slot; this causes each additional vertex buffer in the array to be implicitly bound to each subsequent input slot. The maximum of 16 or 32 input slots (ranges from 0 to D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT - 1) are available; the <a href="https://docs.microsoft.com/windows/desktop/direct3d11/overviews-direct3d-11-devices-downlevel-intro">maximum number of input slots depends on the feature level</a>.</param>
	///       <param name="numBuffers">The number of vertex buffers to get starting at the offset. The number of buffers (plus the starting slot) cannot exceed the total number of IA-stage input slots.</param>
	///       <param name="vertexBuffers">A pointer to an array of vertex buffers returned by the method (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>).</param>
	///       <param name="strides">Pointer to an array of stride values returned by the method; one stride value for each buffer in the vertex-buffer array. Each stride value is the size (in bytes) of the elements that are to be used from that vertex buffer.</param>
	///       <param name="offsets">Pointer to an array of offset values returned by the method; one offset value for each buffer in the vertex-buffer array. Each offset is the number of bytes between the first element of a vertex buffer and the first element that will be used.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IAGetVertexBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppVertexBuffers, [Out, Buffer, Optional] UINT* pStrides, [Out, Buffer, Optional] UINT* pOffsets)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IAGetVertexBuffers</unmanaged-short>
	public unsafe void IAGetVertexBuffers(int startSlot, int numBuffers, ID3D11Buffer[] vertexBuffers, int[] strides, int[] offsets)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (vertexBuffers != null)
		{
			int num = vertexBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = offsets)
		{
			fixed (int* ptr2 = strides)
			{
				fixed (IntPtr* ptr3 = span)
				{
					void* ptr4 = ptr3;
					((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void*, void*, void>)((CppObject)this)[79])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr4, ptr2, ptr);
				}
			}
		}
		if (vertexBuffers != null)
		{
			int i = 0;
			for (int num2 = vertexBuffers.Length; i < num2; i++)
			{
				vertexBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get a pointer to the index buffer that is bound to the input-assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iagetindexbuffer" /></para>
	///       <param name="indexBuffer">A pointer to an index buffer returned by the method (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>).</param>
	///       <param name="format">Specifies format of the data in the index buffer (see <a href="https://docs.microsoft.com/windows/desktop/api/dxgiformat/ne-dxgiformat-dxgi_format">DXGI_FORMAT</a>). These formats provide the size and type of 
	///           the data in the buffer. The only formats allowed for index buffer data are 16-bit (DXGI_FORMAT_R16_UINT) and 32-bit (DXGI_FORMAT_R32_UINT) 
	///           integers.</param>
	///       <param name="offset">Offset (in bytes) from the start of the index buffer, to the first index to use.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IAGetIndexBuffer([Out, Optional] ID3D11Buffer** pIndexBuffer, [Out, Optional] DXGI_FORMAT* Format, [Out, Optional] UINT* Offset)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IAGetIndexBuffer</unmanaged-short>
	public unsafe void IAGetIndexBuffer(out ID3D11Buffer indexBuffer, out Format format, out int offset)
	{
		IntPtr zero = IntPtr.Zero;
		fixed (int* ptr = &offset)
		{
			void* ptr2 = ptr;
			fixed (Format* ptr3 = &format)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[80])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		indexBuffer = ((zero != IntPtr.Zero) ? new ID3D11Buffer(zero) : null);
	}

	/// <summary>
	///       <para>Get the constant buffers used by the geometry shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gsgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSGetConstantBuffers</unmanaged-short>
	public unsafe void GSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[81])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the geometry shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gsgetshader" /></para>
	///       <param name="geometryShader">Address of a pointer to a geometry shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11geometryshader">ID3D11GeometryShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSGetShader([Out, Optional] ID3D11GeometryShader** ppGeometryShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSGetShader</unmanaged-short>
	public unsafe void GSGetShader(out ID3D11GeometryShader geometryShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[82])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		geometryShader = ((zero != IntPtr.Zero) ? new ID3D11GeometryShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get information about the primitive type, and data order that describes input data for the input assembler stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-iagetprimitivetopology" /></para>
	///       <param name="topology">A pointer to the type of primitive, and ordering of the primitive data (see <a href="https://docs.microsoft.com/previous-versions/windows/desktop/legacy/ff476189(v=vs.85)">D3D11_PRIMITIVE_TOPOLOGY</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::IAGetPrimitiveTopology([Out] D3D_PRIMITIVE_TOPOLOGY* pTopology)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::IAGetPrimitiveTopology</unmanaged-short>
	public unsafe PrimitiveTopology IAGetPrimitiveTopology()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		PrimitiveTopology result = default(PrimitiveTopology);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[83])(((CppObject)this).NativePointer, &result);
		return result;
	}

	/// <summary>
	///       <para>Get the vertex shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vsgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSGetShaderResources</unmanaged-short>
	public unsafe void VSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[84])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler states from the vertex shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vsgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Arry of sampler-state interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>) to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSGetSamplers</unmanaged-short>
	public unsafe void VSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[85])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the rendering predicate state.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-getpredication" /></para>
	///       <param name="predicate">Address of a pointer to a predicate (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11predicate">ID3D11Predicate</a>). Value stored here will be <b>NULL</b> upon device creation.</param>
	///       <param name="predicateValue">Address of a boolean to fill with the predicate comparison value. <b>FALSE</b> upon device creation.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GetPredication([Out] ID3D11Predicate** ppPredicate, [Out, Optional] BOOL* pPredicateValue)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetPredication</unmanaged-short>
	public unsafe ID3D11Predicate GetPredication(out RawBool predicateValue)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		predicateValue = default(RawBool);
		fixed (RawBool* ptr = &predicateValue)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[86])(((CppObject)this).NativePointer, &zero, ptr2);
		}
		if (!(zero != IntPtr.Zero))
		{
			return null;
		}
		return new ID3D11Predicate(zero);
	}

	/// <summary>
	///       <para>Get the geometry shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gsgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSGetShaderResources</unmanaged-short>
	public unsafe void GSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[87])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler state interfaces from the geometry shader pipeline stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gsgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSGetSamplers</unmanaged-short>
	public unsafe void GSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[88])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get pointers to the resources bound to the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omgetrendertargets" /></para>
	///       <param name="numViews">Number of render targets to retrieve.</param>
	///       <param name="renderTargetViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rendertargetview">ID3D11RenderTargetView</a>s which represent render target views. Specify <b>NULL</b> for this parameter when retrieval of a render target is not needed.</param>
	///       <param name="depthStencilView">Pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilview">ID3D11DepthStencilView</a>, which represents a depth-stencil view. Specify <b>NULL</b> for this parameter when retrieval of the depth-stencil view is not needed.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMGetRenderTargets([In] UINT NumViews, [Out, Buffer, Optional] ID3D11RenderTargetView** ppRenderTargetViews, [Out, Optional] ID3D11DepthStencilView** ppDepthStencilView)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMGetRenderTargets</unmanaged-short>
	public unsafe void OMGetRenderTargets(int numViews, ID3D11RenderTargetView[] renderTargetViews, out ID3D11DepthStencilView depthStencilView)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (renderTargetViews != null)
		{
			int num = renderTargetViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		IntPtr zero = IntPtr.Zero;
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void*, void>)((CppObject)this)[89])(((CppObject)this).NativePointer, numViews, ptr2, &zero);
		}
		if (renderTargetViews != null)
		{
			int i = 0;
			for (int num2 = renderTargetViews.Length; i < num2; i++)
			{
				renderTargetViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11RenderTargetView(span[i]) : null);
			}
		}
		depthStencilView = ((zero != IntPtr.Zero) ? new ID3D11DepthStencilView(zero) : null);
	}

	/// <summary>
	///       <para>Get pointers to the resources bound to the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omgetrendertargetsandunorderedaccessviews" /></para>
	///       <param name="numRTVs">The number of render-target views to retrieve.</param>
	///       <param name="renderTargetViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rendertargetview">ID3D11RenderTargetView</a>s, which represent render-target views. Specify <b>NULL</b> for this parameter when retrieval of render-target views is not required.</param>
	///       <param name="depthStencilView">Pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilview">ID3D11DepthStencilView</a>, which represents a depth-stencil view. Specify <b>NULL</b> for this parameter when retrieval of the depth-stencil view is not required.</param>
	///       <param name="UAVStartSlot">Index into a zero-based array to begin retrieving unordered-access views (ranges from 0 to D3D11_PS_CS_UAV_REGISTER_COUNT - 1).
	/// For pixel shaders <i>UAVStartSlot</i> should be equal to the number of render-target views that are bound.</param>
	///       <param name="numUAVs">Number of unordered-access views to return in <i>ppUnorderedAccessViews</i>. This number ranges from 0 to D3D11_PS_CS_UAV_REGISTER_COUNT - <i>UAVStartSlot</i>.</param>
	///       <param name="unorderedAccessViews">Pointer to an array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a>s, which represent unordered-access views that are retrieved. Specify <b>NULL</b> for this parameter when retrieval of unordered-access views is not required.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMGetRenderTargetsAndUnorderedAccessViews([In] UINT NumRTVs, [Out, Buffer, Optional] ID3D11RenderTargetView** ppRenderTargetViews, [Out, Optional] ID3D11DepthStencilView** ppDepthStencilView, [In] UINT UAVStartSlot, [In] UINT NumUAVs, [Out, Buffer, Optional] ID3D11UnorderedAccessView** ppUnorderedAccessViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMGetRenderTargetsAndUnorderedAccessViews</unmanaged-short>
	public unsafe void OMGetRenderTargetsAndUnorderedAccessViews(int numRTVs, ID3D11RenderTargetView[] renderTargetViews, out ID3D11DepthStencilView depthStencilView, int uAVStartSlot, int numUAVs, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (renderTargetViews != null)
		{
			int num = renderTargetViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span3 = default(Span<IntPtr>);
		if (unorderedAccessViews != null)
		{
			int num2 = unorderedAccessViews.Length;
			Span<IntPtr> span2 = (((uint)(num2 * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num2]) : stackalloc IntPtr[num2]);
			span3 = span2;
		}
		fixed (IntPtr* ptr = span3)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void*, int, int, void*, void>)((CppObject)this)[90])(((CppObject)this).NativePointer, numRTVs, ptr4, &zero, uAVStartSlot, numUAVs, ptr2);
			}
		}
		if (renderTargetViews != null)
		{
			int i = 0;
			for (int num3 = renderTargetViews.Length; i < num3; i++)
			{
				renderTargetViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11RenderTargetView(span[i]) : null);
			}
		}
		depthStencilView = ((zero != IntPtr.Zero) ? new ID3D11DepthStencilView(zero) : null);
		if (unorderedAccessViews != null)
		{
			int j = 0;
			for (int num4 = unorderedAccessViews.Length; j < num4; j++)
			{
				unorderedAccessViews[j] = ((span3[j] != IntPtr.Zero) ? new ID3D11UnorderedAccessView(span3[j]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the blend state of the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omgetblendstate" /></para>
	///       <param name="blendState">Address of a pointer to a blend-state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11blendstate">ID3D11BlendState</a>).</param>
	///       <param name="blendFactor">Array of blend factors, one for each RGBA component.</param>
	///       <param name="sampleMask">Pointer to a <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-omsetblendstate">sample mask</a>.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMGetBlendState([Out, Optional] ID3D11BlendState** ppBlendState, [Out, Optional] float* BlendFactor, [Out, Optional] UINT* pSampleMask)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMGetBlendState</unmanaged-short>
	private unsafe void OMGetBlendState(out ID3D11BlendState blendState, out float blendFactor, out int sampleMask)
	{
		IntPtr zero = IntPtr.Zero;
		fixed (int* ptr = &sampleMask)
		{
			void* ptr2 = ptr;
			fixed (float* ptr3 = &blendFactor)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[OMGetBlendState__vtbl_index])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		blendState = ((zero != IntPtr.Zero) ? new ID3D11BlendState(zero) : null);
	}

	/// <summary>
	///       <para>Gets the depth-stencil state of the output-merger stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-omgetdepthstencilstate" /></para>
	///       <param name="depthStencilState">Address of a pointer to a depth-stencil state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11depthstencilstate">ID3D11DepthStencilState</a>) to be filled with information from the device.</param>
	///       <param name="stencilRef">Pointer to the stencil reference value used in the depth-stencil test.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::OMGetDepthStencilState([Out, Optional] ID3D11DepthStencilState** ppDepthStencilState, [Out, Optional] UINT* pStencilRef)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::OMGetDepthStencilState</unmanaged-short>
	public unsafe void OMGetDepthStencilState(out ID3D11DepthStencilState depthStencilState, out int stencilRef)
	{
		IntPtr zero = IntPtr.Zero;
		fixed (int* ptr = &stencilRef)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[92])(((CppObject)this).NativePointer, &zero, ptr2);
		}
		depthStencilState = ((zero != IntPtr.Zero) ? new ID3D11DepthStencilState(zero) : null);
	}

	/// <summary>
	///       <para>Get the target output buffers for the stream-output stage of the pipeline.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-sogettargets" /></para>
	///       <param name="numBuffers">Number of buffers to get.</param>
	///       <param name="sOTargets">An array of output buffers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be retrieved from the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::SOGetTargets([In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppSOTargets)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::SOGetTargets</unmanaged-short>
	public unsafe void SOGetTargets(int numBuffers, ID3D11Buffer[] sOTargets)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (sOTargets != null)
		{
			int num = sOTargets.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, void*, void>)((CppObject)this)[93])(((CppObject)this).NativePointer, numBuffers, ptr2);
		}
		if (sOTargets != null)
		{
			int i = 0;
			for (int num2 = sOTargets.Length; i < num2; i++)
			{
				sOTargets[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the rasterizer state from the rasterizer stage of the pipeline.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rsgetstate" /></para>
	///       <param name="rasterizerState">Address of a pointer to a rasterizer-state interface (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11rasterizerstate">ID3D11RasterizerState</a>) to fill with information from the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSGetState([Out, Optional] ID3D11RasterizerState** ppRasterizerState)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSGetState</unmanaged-short>
	public unsafe ID3D11RasterizerState RSGetState()
	{
		IntPtr zero = IntPtr.Zero;
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void>)((CppObject)this)[94])(((CppObject)this).NativePointer, &zero);
		if (!(zero != IntPtr.Zero))
		{
			return null;
		}
		return new ID3D11RasterizerState(zero);
	}

	/// <summary>
	///       <para>Gets the array of viewports bound to the rasterizer stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rsgetviewports" /></para>
	///       <param name="numViewports">A pointer to a variable that, on input, specifies the number of viewports (ranges from 0 to <b>D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE</b>)
	/// in the <i>pViewports</i> array; on output, the variable contains the actual number of viewports that are bound to the rasterizer stage.
	/// If <i>pViewports</i> is <b>NULL</b>, <b>RSGetViewports</b> fills the variable with the number of viewports currently bound.
	///
	/// <div class="alert"><b>Note</b>  In some versions of the Windows SDK, a <a href="https://docs.microsoft.com/windows/desktop/direct3d11/overviews-direct3d-11-devices-layers">debug device</a> will raise an exception if the input value in the variable to which <i>pNumViewports</i> points is greater than <b>D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE</b> even if <i>pViewports</i> is <b>NULL</b>.  The regular runtime ignores the value in the variable to which <i>pNumViewports</i> points when <i>pViewports</i> is <b>NULL</b>.  This behavior of a debug device might be corrected in a future release of the Windows SDK.
	/// </div>
	/// <div> </div></param>
	///       <param name="viewports">An array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/ns-d3d11-d3d11_viewport">D3D11_VIEWPORT</a> structures for the viewports that are bound to the rasterizer stage. If the number of viewports (in the variable to which <i>pNumViewports</i> points) is
	/// greater than the actual number of viewports currently bound, unused elements of the array contain 0.
	/// For info about how the viewport size depends on the device <a href="https://docs.microsoft.com/windows/desktop/direct3d11/overviews-direct3d-11-devices-downlevel-intro">feature level</a>, which has changed between Direct3D 11
	/// and Direct3D 10, see <b>D3D11_VIEWPORT</b>.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSGetViewports([InOut] UINT* pNumViewports, [Out, Buffer, Optional] D3D11_VIEWPORT* pViewports)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSGetViewports</unmanaged-short>
	internal unsafe void RSGetViewports(ref int numViewports, void* viewports)
	{
		fixed (int* ptr = &numViewports)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[95])(((CppObject)this).NativePointer, ptr2, viewports);
		}
	}

	/// <summary>
	///       <para>Get the array of scissor rectangles bound to the rasterizer stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-rsgetscissorrects" /></para>
	///       <param name="numRects">The number of scissor rectangles (ranges between 0 and D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE) bound; set <i>pRects</i> to <b>NULL</b> to use <i>pNumRects</i> to see how many rectangles would be returned.</param>
	///       <param name="rects">An array of scissor rectangles (see <a href="https://docs.microsoft.com/windows/desktop/direct3d11/d3d11-rect">D3D11_RECT</a>). If NumRects is greater than the number of scissor rects currently bound, then unused members of the array will contain 0.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::RSGetScissorRects([InOut] UINT* pNumRects, [Out, Buffer, Optional] RECT* pRects)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::RSGetScissorRects</unmanaged-short>
	internal unsafe void RSGetScissorRects(ref int numRects, IntPtr rects)
	{
		fixed (int* ptr = &numRects)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void>)((CppObject)this)[96])(((CppObject)this).NativePointer, ptr2, (void*)rects);
		}
	}

	/// <summary>
	///       <para>Get the hull-shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hsgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSGetShaderResources</unmanaged-short>
	public unsafe void HSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[97])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the hull shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hsgetshader" /></para>
	///       <param name="hullShader">Address of a pointer to a hull shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11hullshader">ID3D11HullShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSGetShader([Out, Optional] ID3D11HullShader** ppHullShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSGetShader</unmanaged-short>
	public unsafe void HSGetShader(out ID3D11HullShader hullShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[98])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		hullShader = ((zero != IntPtr.Zero) ? new ID3D11HullShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler state interfaces from the hull-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hsgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSGetSamplers</unmanaged-short>
	public unsafe void HSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[99])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the constant buffers used by the hull-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hsgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSGetConstantBuffers</unmanaged-short>
	public unsafe void HSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[100])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the domain-shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dsgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSGetShaderResources</unmanaged-short>
	public unsafe void DSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[101])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the domain shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dsgetshader" /></para>
	///       <param name="domainShader">Address of a pointer to a domain shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11domainshader">ID3D11DomainShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSGetShader([Out, Optional] ID3D11DomainShader** ppDomainShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSGetShader</unmanaged-short>
	public unsafe void DSGetShader(out ID3D11DomainShader domainShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[102])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		domainShader = ((zero != IntPtr.Zero) ? new ID3D11DomainShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler state interfaces from the domain-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dsgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSGetSamplers</unmanaged-short>
	public unsafe void DSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[103])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the constant buffers used by the domain-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dsgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSGetConstantBuffers</unmanaged-short>
	public unsafe void DSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[104])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the compute-shader resources.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-csgetshaderresources" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin getting shader resources from (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1).</param>
	///       <param name="numViews">The number of resources to get from the device. Up to a maximum of 128 slots are available for shader resources (ranges from 0 to D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot).</param>
	///       <param name="shaderResourceViews">Array of <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11shaderresourceview">shader resource view</a> interfaces to be returned by the device.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSGetShaderResources([In] UINT StartSlot, [In] UINT NumViews, [Out, Buffer, Optional] ID3D11ShaderResourceView** ppShaderResourceViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSGetShaderResources</unmanaged-short>
	public unsafe void CSGetShaderResources(int startSlot, int numViews, ID3D11ShaderResourceView[] shaderResourceViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (shaderResourceViews != null)
		{
			int num = shaderResourceViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[105])(((CppObject)this).NativePointer, startSlot, numViews, ptr2);
		}
		if (shaderResourceViews != null)
		{
			int i = 0;
			for (int num2 = shaderResourceViews.Length; i < num2; i++)
			{
				shaderResourceViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ShaderResourceView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Gets an array of views for an unordered resource.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-csgetunorderedaccessviews" /></para>
	///       <param name="startSlot">Index of the first element in the zero-based array to return (ranges from 0 to D3D11_1_UAV_SLOT_COUNT - 1).</param>
	///       <param name="numUAVs">Number of views to get (ranges from 0 to D3D11_1_UAV_SLOT_COUNT - StartSlot).</param>
	///       <param name="unorderedAccessViews">A pointer to an array of interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11unorderedaccessview">ID3D11UnorderedAccessView</a>) to get.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSGetUnorderedAccessViews([In] UINT StartSlot, [In] UINT NumUAVs, [Out, Buffer, Optional] ID3D11UnorderedAccessView** ppUnorderedAccessViews)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSGetUnorderedAccessViews</unmanaged-short>
	public unsafe void CSGetUnorderedAccessViews(int startSlot, int numUAVs, ID3D11UnorderedAccessView[] unorderedAccessViews)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (unorderedAccessViews != null)
		{
			int num = unorderedAccessViews.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[106])(((CppObject)this).NativePointer, startSlot, numUAVs, ptr2);
		}
		if (unorderedAccessViews != null)
		{
			int i = 0;
			for (int num2 = unorderedAccessViews.Length; i < num2; i++)
			{
				unorderedAccessViews[i] = ((span[i] != IntPtr.Zero) ? new ID3D11UnorderedAccessView(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the compute shader currently set on the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-csgetshader" /></para>
	///       <param name="computeShader">Address of a pointer to a Compute shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11computeshader">ID3D11ComputeShader</a>) to be returned by the method.</param>
	///       <param name="classInstances">Pointer to an array of class instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>).</param>
	///       <param name="numClassInstances">The number of class-instance elements in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSGetShader([Out, Optional] ID3D11ComputeShader** ppComputeShader, [Out, Buffer, Optional] ID3D11ClassInstance** ppClassInstances, [InOut] UINT* pNumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSGetShader</unmanaged-short>
	public unsafe void CSGetShader(out ID3D11ComputeShader computeShader, ID3D11ClassInstance[] classInstances, ref int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		Span<IntPtr> span = default(Span<IntPtr>);
		if (classInstances != null)
		{
			int num = classInstances.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (int* ptr = &numClassInstances)
		{
			void* ptr2 = ptr;
			fixed (IntPtr* ptr3 = span)
			{
				void* ptr4 = ptr3;
				((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, void*, void>)((CppObject)this)[107])(((CppObject)this).NativePointer, &zero, ptr4, ptr2);
			}
		}
		computeShader = ((zero != IntPtr.Zero) ? new ID3D11ComputeShader(zero) : null);
		if (classInstances != null)
		{
			int i = 0;
			for (int num2 = classInstances.Length; i < num2; i++)
			{
				classInstances[i] = ((span[i] != IntPtr.Zero) ? new ID3D11ClassInstance(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get an array of sampler state interfaces from the compute-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-csgetsamplers" /></para>
	///       <param name="startSlot">Index into a zero-based array to begin getting samplers from (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1).</param>
	///       <param name="numSamplers">Number of samplers to get from a device context. Each pipeline stage has a total of 16 sampler slots available (ranges from 0 to D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot).</param>
	///       <param name="samplers">Pointer to an array of sampler-state interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11samplerstate">ID3D11SamplerState</a>).</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSGetSamplers([In] UINT StartSlot, [In] UINT NumSamplers, [Out, Buffer, Optional] ID3D11SamplerState** ppSamplers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSGetSamplers</unmanaged-short>
	public unsafe void CSGetSamplers(int startSlot, int numSamplers, ID3D11SamplerState[] samplers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (samplers != null)
		{
			int num = samplers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[108])(((CppObject)this).NativePointer, startSlot, numSamplers, ptr2);
		}
		if (samplers != null)
		{
			int i = 0;
			for (int num2 = samplers.Length; i < num2; i++)
			{
				samplers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11SamplerState(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Get the constant buffers used by the compute-shader stage.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-csgetconstantbuffers" /></para>
	///       <param name="startSlot">Index into the device's zero-based array to begin retrieving constant buffers from (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1).</param>
	///       <param name="numBuffers">Number of buffers to retrieve (ranges from 0 to D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot).</param>
	///       <param name="constantBuffers">Array of constant buffer interface pointers (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11buffer">ID3D11Buffer</a>) to be returned by the method.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSGetConstantBuffers([In] UINT StartSlot, [In] UINT NumBuffers, [Out, Buffer, Optional] ID3D11Buffer** ppConstantBuffers)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSGetConstantBuffers</unmanaged-short>
	public unsafe void CSGetConstantBuffers(int startSlot, int numBuffers, ID3D11Buffer[] constantBuffers)
	{
		Span<IntPtr> span = default(Span<IntPtr>);
		if (constantBuffers != null)
		{
			int num = constantBuffers.Length;
			Span<IntPtr> span2 = (((uint)(num * IntPtr.Size) >= 1024u) ? ((Span<IntPtr>)new IntPtr[num]) : stackalloc IntPtr[num]);
			span = span2;
		}
		fixed (IntPtr* ptr = span)
		{
			void* ptr2 = ptr;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int, void*, void>)((CppObject)this)[109])(((CppObject)this).NativePointer, startSlot, numBuffers, ptr2);
		}
		if (constantBuffers != null)
		{
			int i = 0;
			for (int num2 = constantBuffers.Length; i < num2; i++)
			{
				constantBuffers[i] = ((span[i] != IntPtr.Zero) ? new ID3D11Buffer(span[i]) : null);
			}
		}
	}

	/// <summary>
	///       <para>Restore all default settings.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-clearstate" /></para>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::ClearState()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::ClearState</unmanaged-short>
	public unsafe void ClearState()
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void>)((CppObject)this)[110])(((CppObject)this).NativePointer);
	}

	/// <summary>
	///       <para>Sends queued-up commands in the command buffer to the graphics processing unit (GPU).</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-flush" /></para>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::Flush()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::Flush</unmanaged-short>
	public unsafe void Flush()
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void>)((CppObject)this)[111])(((CppObject)this).NativePointer);
	}

	/// <summary>
	///       <para>Gets the type of device context.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gettype" /></para>
	///     </summary>
	/// <unmanaged>D3D11_DEVICE_CONTEXT_TYPE ID3D11DeviceContext::GetType()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetType</unmanaged-short>
	internal unsafe DeviceContextType GetContextType()
	{
		return ((delegate* unmanaged[Stdcall]<IntPtr, DeviceContextType>)((CppObject)this)[112])(((CppObject)this).NativePointer);
	}

	/// <summary>
	///       <para>Gets the initialization flags associated with the current deferred context.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-getcontextflags" /></para>
	///     </summary>
	/// <unmanaged>UINT ID3D11DeviceContext::GetContextFlags()</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GetContextFlags</unmanaged-short>
	internal unsafe int GetContextFlags()
	{
		return ((delegate* unmanaged[Stdcall]<IntPtr, int>)((CppObject)this)[113])(((CppObject)this).NativePointer);
	}

	/// <summary>
	///       <para>Create a command list and record graphics commands into it.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-finishcommandlist" /></para>
	///       <param name="restoreDeferredContextState">A Boolean flag that determines whether the runtime saves deferred context state before it executes  <b>FinishCommandList</b> and restores it afterwards. Use <b>TRUE</b> to indicate that the runtime needs to save and restore the state. Use <b>FALSE</b> to indicate that the runtime will not save or restore any state. In this case, the deferred context will  return to its default state after the call to  <b>FinishCommandList</b> completes. For information about default state, see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-clearstate">ID3D11DeviceContext::ClearState</a>. Typically, use <b>FALSE</b> unless you restore the state to be nearly equivalent to the state that the runtime would restore if you passed <b>TRUE</b>. When you use <b>FALSE</b>, you can avoid unnecessary and inefficient state transitions.
	///
	///
	/// <div class="alert"><b>Note</b>  This parameter does not affect the command list that the current call to <b>FinishCommandList</b> returns. However, this parameter affects the command list of the next call to <b>FinishCommandList</b> on the same deferred context.
	/// </div>
	/// <div> </div></param>
	///       <param name="commandList">Upon completion of the method, the passed pointer to an <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11commandlist">ID3D11CommandList</a> interface pointer is initialized with the recorded command list information. The resulting <b>ID3D11CommandList</b> object is immutable and can only be used with <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nf-d3d11-id3d11devicecontext-executecommandlist">ID3D11DeviceContext::ExecuteCommandList</a>.</param>
	///     </summary>
	/// <unmanaged>HRESULT ID3D11DeviceContext::FinishCommandList([In] BOOL RestoreDeferredContextState, [Out, Optional] ID3D11CommandList** ppCommandList)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::FinishCommandList</unmanaged-short>
	public unsafe Result FinishCommandList(RawBool restoreDeferredContextState, out ID3D11CommandList commandList)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		IntPtr zero = IntPtr.Zero;
		Result result = Result.op_Implicit(((delegate* unmanaged[Stdcall]<IntPtr, RawBool, void*, int>)((CppObject)this)[114])(((CppObject)this).NativePointer, restoreDeferredContextState, &zero));
		commandList = ((zero != IntPtr.Zero) ? new ID3D11CommandList(zero) : null);
		return result;
	}

	/// <summary>
	///       <para>Sets a pixel shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetshader" /></para>
	///       <param name="pixelShader">Pointer to a pixel shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11pixelshader">ID3D11PixelShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetShader([In, Optional] ID3D11PixelShader* pPixelShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetShader</unmanaged-short>
	public unsafe void PSSetShader(ID3D11PixelShader pixelShader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((pixelShader != null) ? ((CppObject)pixelShader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[9])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(pixelShader);
	}

	/// <summary>
	///       <para>Sets a pixel shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-pssetshader" /></para>
	///       <param name="pixelShader">Pointer to a pixel shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11pixelshader">ID3D11PixelShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::PSSetShader([In, Optional] ID3D11PixelShader* pPixelShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::PSSetShader</unmanaged-short>
	private unsafe void PSSetShader(IntPtr pixelShader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[9])(((CppObject)this).NativePointer, (void*)pixelShader, (void*)classInstances, numClassInstances);
	}

	/// <summary>
	///       <para>Set a vertex shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetshader" /></para>
	///       <param name="vertexShader">Pointer to a vertex shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11vertexshader">ID3D11VertexShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetShader([In, Optional] ID3D11VertexShader* pVertexShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetShader</unmanaged-short>
	public unsafe void VSSetShader(ID3D11VertexShader vertexShader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((vertexShader != null) ? ((CppObject)vertexShader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[11])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(vertexShader);
	}

	/// <summary>
	///       <para>Set a vertex shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-vssetshader" /></para>
	///       <param name="vertexShader">Pointer to a vertex shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11vertexshader">ID3D11VertexShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::VSSetShader([In, Optional] ID3D11VertexShader* pVertexShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::VSSetShader</unmanaged-short>
	private unsafe void VSSetShader(IntPtr vertexShader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[11])(((CppObject)this).NativePointer, (void*)vertexShader, (void*)classInstances, numClassInstances);
	}

	/// <summary>
	///       <para>Set a geometry shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetshader" /></para>
	///       <param name="shader">Pointer to a geometry shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11geometryshader">ID3D11GeometryShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetShader([In, Optional] ID3D11GeometryShader* pShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetShader</unmanaged-short>
	public unsafe void GSSetShader(ID3D11GeometryShader shader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((shader != null) ? ((CppObject)shader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[23])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(shader);
	}

	/// <summary>
	///       <para>Set a geometry shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-gssetshader" /></para>
	///       <param name="shader">Pointer to a geometry shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11geometryshader">ID3D11GeometryShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::GSSetShader([In, Optional] ID3D11GeometryShader* pShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::GSSetShader</unmanaged-short>
	private unsafe void GSSetShader(IntPtr shader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[23])(((CppObject)this).NativePointer, (void*)shader, (void*)classInstances, numClassInstances);
	}

	/// <summary>
	///       <para>Set a hull shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetshader" /></para>
	///       <param name="hullShader">Pointer to a hull shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11hullshader">ID3D11HullShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetShader([In, Optional] ID3D11HullShader* pHullShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetShader</unmanaged-short>
	public unsafe void HSSetShader(ID3D11HullShader hullShader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((hullShader != null) ? ((CppObject)hullShader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[60])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(hullShader);
	}

	/// <summary>
	///       <para>Set a hull shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-hssetshader" /></para>
	///       <param name="hullShader">Pointer to a hull shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11hullshader">ID3D11HullShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::HSSetShader([In, Optional] ID3D11HullShader* pHullShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::HSSetShader</unmanaged-short>
	private unsafe void HSSetShader(IntPtr hullShader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[60])(((CppObject)this).NativePointer, (void*)hullShader, (void*)classInstances, numClassInstances);
	}

	/// <summary>
	///       <para>Set a domain shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetshader" /></para>
	///       <param name="domainShader">Pointer to a domain shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11domainshader">ID3D11DomainShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetShader([In, Optional] ID3D11DomainShader* pDomainShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetShader</unmanaged-short>
	public unsafe void DSSetShader(ID3D11DomainShader domainShader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((domainShader != null) ? ((CppObject)domainShader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[64])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(domainShader);
	}

	/// <summary>
	///       <para>Set a domain shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-dssetshader" /></para>
	///       <param name="domainShader">Pointer to a domain shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11domainshader">ID3D11DomainShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::DSSetShader([In, Optional] ID3D11DomainShader* pDomainShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::DSSetShader</unmanaged-short>
	private unsafe void DSSetShader(IntPtr domainShader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[64])(((CppObject)this).NativePointer, (void*)domainShader, (void*)classInstances, numClassInstances);
	}

	/// <summary>
	///       <para>Set a compute shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetshader" /></para>
	///       <param name="computeShader">Pointer to a compute shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11computeshader">ID3D11ComputeShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetShader([In, Optional] ID3D11ComputeShader* pComputeShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetShader</unmanaged-short>
	public unsafe void CSSetShader(ID3D11ComputeShader computeShader, InterfaceArray<ID3D11ClassInstance> classInstances, int numClassInstances)
	{
		IntPtr zero = IntPtr.Zero;
		zero = ((computeShader != null) ? ((CppObject)computeShader).NativePointer : IntPtr.Zero);
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[69])(((CppObject)this).NativePointer, (void*)zero, classInstances.NativePointer, numClassInstances);
		GC.KeepAlive(computeShader);
	}

	/// <summary>
	///       <para>Set a compute shader to the device.</para>
	///       <para>Microsoft Docs: <see href="https://docs.microsoft.com/windows/win32/api//d3d11/nf-d3d11-id3d11devicecontext-cssetshader" /></para>
	///       <param name="computeShader">Pointer to a compute shader (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11computeshader">ID3D11ComputeShader</a>). Passing in <b>NULL</b> disables the shader for this pipeline stage.</param>
	///       <param name="classInstances">A pointer to an array of class-instance interfaces (see <a href="https://docs.microsoft.com/windows/desktop/api/d3d11/nn-d3d11-id3d11classinstance">ID3D11ClassInstance</a>). Each interface used by a shader must have a corresponding class instance or the shader will get disabled. Set ppClassInstances to <b>NULL</b> if the shader does not use any interfaces.</param>
	///       <param name="numClassInstances">The number of class-instance interfaces in the array.</param>
	///     </summary>
	/// <unmanaged>void ID3D11DeviceContext::CSSetShader([In, Optional] ID3D11ComputeShader* pComputeShader, [In, Buffer, Optional] const ID3D11ClassInstance** ppClassInstances, [In] UINT NumClassInstances)</unmanaged>
	/// <unmanaged-short>ID3D11DeviceContext::CSSetShader</unmanaged-short>
	private unsafe void CSSetShader(IntPtr computeShader, IntPtr classInstances, int numClassInstances)
	{
		((delegate* unmanaged[Stdcall]<IntPtr, void*, void*, int, void>)((CppObject)this)[69])(((CppObject)this).NativePointer, (void*)computeShader, (void*)classInstances, numClassInstances);
	}
}
