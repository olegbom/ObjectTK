using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DerpGL.Shaders.Variables;
using log4net;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace DerpGL.Shaders
{
    /// <summary>
    /// Provides a base class for shader programs.<br/>
    /// Automatically maps properties to shader variables by name.<br/>
    /// For shader variable types see:<br/>
    /// <see cref="VertexAttrib"/>, <see cref="Uniform{T}"/>, <see cref="TextureUniform"/>, <see cref="ImageUniform"/>,
    /// <see cref="UniformBuffer"/>, <see cref="TransformOut"/>, <see cref="ShaderStorage"/>
    /// </summary>
    public class Shader
        : GLResource
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Shader));

        /// <summary>
        /// Path used when looking for shader files.
        /// </summary>
        public static string BasePath { get; set; }

        /// <summary>
        /// Handle of the currently active shader program.
        /// </summary>
        public static int ActiveProgramHandle { get; protected set; }

        /// <summary>
        /// Defines the <see cref="TransformFeedbackMode"/> which is used to specify how data should be written to the output buffer(s).
        /// May be overwritten by derived classes to be changed.
        /// </summary>
        protected virtual TransformFeedbackMode TransformOutMode
        {
            get { return TransformFeedbackMode.SeparateAttribs; }
        }

        protected Shader()
            : base(GL.CreateProgram())
        {
            BasePath = "Data/Shaders/";
            var shaderSources = ShaderSourceAttribute.GetShaderSources(this);
            if (shaderSources.Count == 0) throw new ApplicationException("ShaderSourceAttribute(s) missing!");
            try
            {
                CreateProgram(shaderSources);
            }
            catch (FileNotFoundException ex)
            {
                Logger.Error("Shader file not found.", ex);
                throw;
            }
        }

        /// <summary>
        /// Activates the shader program.
        /// </summary>
        public void Use()
        {
            // remember handle of the active program
            ActiveProgramHandle = Handle;
            // activate program
            GL.UseProgram(Handle);
            // disable all vertex attributes
            VertexAttrib.DisableVertexAttribArrays();
        }

        private void CreateProgram(Dictionary<ShaderType, string> shaders)
        {
            Logger.InfoFormat("Creating shader program: {0}", GetType().Name);
            // load and attach all specified shaders
            var shaderObjects = shaders.Select(_ => AttachShader(_.Key, _.Value)).ToList();
            // bind transform feedback varyings if any
            var outs = InitializeTransformOut();
            if (outs.Count > 0)
            {
                GL.TransformFeedbackVaryings(Handle, outs.Count, outs.ToArray(), TransformOutMode);
            }
            // link program
            Logger.DebugFormat("Linking shader program: {0}", GetType().Name);
            GL.LinkProgram(Handle);
            // release shader objects, after they are linked into the program
            shaderObjects.ForEach(GL.DeleteShader);
            // assert that no link errors occured
            int linkStatus;
            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out linkStatus);
            var info = GL.GetProgramInfoLog(Handle);
            Logger.DebugFormat("Link status: {0}", linkStatus);
            if (!string.IsNullOrEmpty(info)) Logger.InfoFormat("Link log:\n{0}", info);
            if (linkStatus != 1)
            {
                var msg = string.Format("Error linking program: {0}", GetType().Name);
                Logger.Error(msg);
                throw new ApplicationException(msg);
            }
            // initialize shader properties
            InitializePropertyMapping();
        }

        private int AttachShader(ShaderType type, string name)
        {
            Logger.DebugFormat("Compiling {0}: {1}", type, name);
            // create shader
            var shader = GL.CreateShader(type);
            // load shaders source
            GL.ShaderSource(shader, RetrieveShaderSource(Path.Combine(BasePath, name)));
            // compile shader
            GL.CompileShader(shader);
            // assert that no compile error occured
            int compileStatus;
            GL.GetShader(shader, ShaderParameter.CompileStatus, out compileStatus);
            Logger.DebugFormat("Compiling status: {0}", compileStatus);
            var info = GL.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(info)) Logger.InfoFormat("Compile log for {0}:\n{1}", name, info);
            if (compileStatus != 1)
            {
                var msg = string.Format("Error compiling shader: {0}", name);
                Logger.Error(msg);
                throw new ApplicationException(msg);
            }
            // attach shader to program
            GL.AttachShader(Handle, shader);
            return shader;
        }

        private static string RetrieveShaderSource(string file, List<string> includedNames = null)
        {
            const string includeKeyword = "#include";
            if (includedNames == null) includedNames = new List<string>();
            // check for multiple includes of the same file
            if (includedNames.Contains(file))
            {
                Logger.WarnFormat("File already included: {0} (included files: {1})", file, string.Join(", ", includedNames));
                return "";
            }
            includedNames.Add(file);
            // load shaders source
            var source = new StringBuilder();
            // parse this file
            using (var reader = new StreamReader(file + ".glsl"))
            {
                var fileNumber = includedNames.Count - 1;
                var lineNumber = 1;
                while (!reader.EndOfStream)
                {
                    // read the file line by line
                    var code = reader.ReadLine();
                    if (code == null) break;
                    // check if it is an include statement
                    if (!code.StartsWith(includeKeyword))
                    {
                        source.AppendLine(code);
                    }
                    else
                    {
                        // parse the included filename
                        var includedFile = code.Remove(0, includeKeyword.Length).Trim();
                        // insert #line statement to correct line numbers
                        source.AppendLine(string.Format("#line 1 {0}", includedNames.Count));
                        // replace current line with the source of that file
                        includedFile = Path.Combine(Path.GetDirectoryName(file), includedFile);
                        source.Append(RetrieveShaderSource(includedFile, includedNames));
                        // the #line statement defines the line number of the *next* line
                        source.AppendLine(string.Format("#line {0} {1}", lineNumber+1, fileNumber));
                    }
                    lineNumber++;
                }
            }
            return source.ToString();
        }

        /// <summary>
        /// Initializes all properties of the current shader which are of type TransformOut.
        /// Is called before linking the shader to initialize transform feedback outputs and store their index to the related property.
        /// </summary>
        /// <returns></returns>
        private List<string> InitializeTransformOut()
        {
            var outs = new List<string>();
            var index = 0;
            foreach (var property in GetType().GetProperties().Where(_ => _.PropertyType == typeof(TransformOut)))
            {
                property.SetValue(this, new TransformOut(index++), null);
                outs.Add(property.Name);
            }
            return outs;
        }

        /// <summary>
        /// Initializes all properties of the current shader instance which are of a type contained in the TypeMapping.
        /// Is called after linking the shader to initialize vertex attributes and uniforms.
        /// </summary>
        private void InitializePropertyMapping()
        {
            foreach (var property in GetType().GetProperties())
            {
                var mapping = TypeMapping.FirstOrDefault(_ => _.MappedType == property.PropertyType);
                if (mapping == null) continue;
                Logger.DebugFormat("Creating property mapping: {0}", property.Name);
                property.SetValue(this, mapping.Create(Handle, property), null);
            }
        }

        protected override void Dispose(bool manual)
        {
            if (!manual) return;
            GL.DeleteProgram(Handle);
        }

        /// <summary>
        /// Defines available property mappings.<br/>
        /// NOTE: maybe refactor to use GL.ProgramUniform* later (check out EXT_direct_state_access)
        /// </summary>
        private static readonly List<IMapping> TypeMapping = new List<IMapping>
        {
            new Mapping<VertexAttrib>(VertexAttribMapping),
            new Mapping<TextureUniform>((p,i) => new TextureUniform(p, i.Name)),
            new Mapping<ImageUniform>((p,i) => new ImageUniform(p, i.Name)),
            new Mapping<Uniform<bool>>((p,i) => new Uniform<bool>(p, i.Name, (l,b) => GL.Uniform1(l, b?1:0))),
            new Mapping<Uniform<int>>((p,i) => new Uniform<int>(p, i.Name, GL.Uniform1)),
            new Mapping<Uniform<uint>>((p,i) => new Uniform<uint>(p, i.Name, GL.Uniform1)),
            new Mapping<Uniform<float>>((p,i) => new Uniform<float>(p, i.Name, GL.Uniform1)),
            new Mapping<Uniform<Vector2>>((p,i) => new Uniform<Vector2>(p, i.Name, GL.Uniform2)),
            new Mapping<Uniform<Vector3>>((p,i) => new Uniform<Vector3>(p, i.Name, GL.Uniform3)),
            new Mapping<Uniform<Vector4>>((p,i) => new Uniform<Vector4>(p, i.Name, GL.Uniform4)),
            new Mapping<Uniform<Matrix4>>((p,i) => new Uniform<Matrix4>(p, i.Name, (_, matrix) => GL.UniformMatrix4(_, false, ref matrix))),
            new Mapping<UniformBuffer>((p,i) => new UniformBuffer(p, i.Name)),
            new Mapping<ShaderStorage>((p,i) => new ShaderStorage(p, i.Name)),
            new Mapping<FragData>((p,i) => new FragData(p, i.Name))
        };

        private static VertexAttrib VertexAttribMapping(int program, MemberInfo info)
        {
            var attrib = info.GetCustomAttributes<VertexAttribAttribute>(false).FirstOrDefault();
            if (attrib == null) throw new ApplicationException("VertexAttribAttribute missing!");
            return new VertexAttrib(program, info.Name, attrib);
        }

        /// <summary>
        /// Enables registration of additional property mappings.<br/>
        /// Mapped types provide automatic property initialization on instantiation.
        /// </summary>
        /// <param name="mapping">The property mapping to add.</param>
        public void AddPropertyMapping(IMapping mapping)
        {
            TypeMapping.Add(mapping);
        }
    }
}