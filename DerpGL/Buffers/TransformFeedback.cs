﻿#region License
// DerpGL License
// Copyright (C) 2013-2014 J.C.Bernack
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion
using System;
using DerpGL.Exceptions;
using DerpGL.Shaders.Variables;
using OpenTK.Graphics.OpenGL;

namespace DerpGL.Buffers
{
    /// <summary>
    /// Represents a transform feedback object.
    /// </summary>
    public class TransformFeedback
        : GLObject
    {
        /// <summary>
        /// Creates a new transform feedback buffer.
        /// </summary>
        public TransformFeedback()
            : base(GL.GenTransformFeedback())
        {
        }

        protected override void Dispose(bool manual)
        {
            if (!manual) return;
            GL.DeleteTransformFeedback(Handle);
        }

        /// <summary>
        /// Binds the transform feedback buffer.
        /// </summary>
        public void Bind()
        {
            GL.BindTransformFeedback(TransformFeedbackTarget.TransformFeedback, Handle);
        }

        /// <summary>
        /// Unbinds the transform feedback buffer.
        /// </summary>
        public void UnBind()
        {
            AssertActive();
            GL.BindTransformFeedback(TransformFeedbackTarget.TransformFeedback, 0);
        }

        /// <summary>
        /// Start transform feedback operation. 
        /// </summary>
        /// <param name="primitiveMode">Specify the output type of the primitives that will be recorded into the buffer objects that are bound for transform feedback.</param>
        public void Begin(TransformFeedbackPrimitiveType primitiveMode)
        {
            AssertActive();
            GL.BeginTransformFeedback(primitiveMode);
        }

        /// <summary>
        /// End transform feedback operation.
        /// </summary>
        public void End()
        {
            AssertActive();
            GL.EndTransformFeedback();
        }

        /// <summary>
        /// Pause transform feedback operations.
        /// </summary>
        public void Pause()
        {
            AssertActive();
            GL.PauseTransformFeedback();
        }

        /// <summary>
        /// Resume transform feedback operations.
        /// </summary>
        public void Resume()
        {
            AssertActive();
            GL.ResumeTransformFeedback();
        }

        /// <summary>
        /// Binds a buffer to the binding index utilized by the given transform feeedback varying.
        /// </summary>
        /// <param name="transformOut">Specifies a transform feedback varying.</param>
        /// <param name="buffer">Specifies a buffer object to bind.</param>
        public void BindOutput<T>(TransformOut transformOut, Buffer<T> buffer)
            where T : struct
        {
            AssertActive();
            GL.BindBufferBase(BufferRangeTarget.TransformFeedbackBuffer, transformOut.Index, buffer.Handle);
        }

        /// <summary>
        /// Binds part of a buffer to the binding index utilized by the given transform feeedback varying.
        /// </summary>
        /// <remarks>
        /// If multiple parts of the same buffer are bound as transform feedback output targets they must not overlap.
        /// </remarks>
        /// <param name="transformOut">Specifies a transform feedback varying.</param>
        /// <param name="buffer">Specifies a buffer object to bind.</param>
        /// <param name="offset">Specifies the starting offset in bytes into the buffer object.</param>
        /// <param name="size">Specifies the amount of data in bytes that can be written to the buffer.</param>
        public void BindOutput<T>(TransformOut transformOut, Buffer<T> buffer, int offset, int size)
            where T : struct
        {
            AssertActive();
            GL.BindBufferRange(BufferRangeTarget.TransformFeedbackBuffer, transformOut.Index, buffer.Handle, (IntPtr)offset, (IntPtr)size);
        }

        /// <summary>
        /// Throws an <see cref="ObjectNotBoundException"/> if this vertex array is not the currently active one.
        /// </summary>
        public void AssertActive()
        {
#if DEBUG
            int activeHandle;
            GL.GetInteger(GetPName.TransformFeedbackBinding, out activeHandle);
            if (activeHandle != Handle) throw new ObjectNotBoundException("Transform feedback object is not bound.");
#endif
        }
    }
}