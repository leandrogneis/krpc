using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using KRPC.Utils;

namespace KRPC.Service.Scanner
{
    sealed class ServiceSignature : ISerializable
    {
        /// <summary>
        /// The name of the service
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Documentation for the service
        /// </summary>
        public string Documentation { get; private set; }

        /// <summary>
        /// A mapping from procedure names to signatures for all RPCs in this service
        /// </summary>
        public Dictionary<string,ProcedureSignature> Procedures { get; private set; }

        /// <summary>
        /// The classes defined in this service
        /// </summary>
        public Dictionary<string,ClassSignature> Classes { get; private set; }

        /// <summary>
        /// The enumerations defined in this service, and their allowed values
        /// </summary>
        public Dictionary<string,EnumerationSignature> Enumerations { get; private set; }

        /// <summary>
        /// The exceptions defined in this service
        /// </summary>
        public Dictionary<string,ExceptionSignature> Exceptions { get; private set; }

        /// <summary>
        /// Which game scene(s) the service should be active during
        /// </summary>
        public GameScene GameScene { get; private set; }

        /// <summary>
        /// Create a service signature from a C# type annotated with the KRPCService attribute
        /// </summary>
        /// <param name="type">Type.</param>
        public ServiceSignature (Type type)
        {
            TypeUtils.ValidateKRPCService (type);
            Name = TypeUtils.GetServiceName (type);
            Documentation = DocumentationUtils.ResolveCrefs (type.GetDocumentation ());
            Classes = new Dictionary<string, ClassSignature> ();
            Enumerations = new Dictionary<string, EnumerationSignature> ();
            Procedures = new Dictionary<string, ProcedureSignature> ();
            Exceptions = new Dictionary<string, ExceptionSignature> ();
            GameScene = TypeUtils.GetServiceGameScene (type);
        }

        /// <summary>
        /// Create a service with the given name.
        /// </summary>
        public ServiceSignature (string name)
        {
            TypeUtils.ValidateIdentifier (name);
            Name = name;
            Documentation = string.Empty;
            Classes = new Dictionary<string, ClassSignature> ();
            Enumerations = new Dictionary<string, EnumerationSignature> ();
            Procedures = new Dictionary<string, ProcedureSignature> ();
            Exceptions = new Dictionary<string, ExceptionSignature> ();
            GameScene = GameScene.All;
        }

        /// <summary>
        /// Add a procedure to the service
        /// </summary>
        void AddProcedure (ProcedureSignature signature)
        {
            if (Procedures.ContainsKey (signature.Name))
                throw new ServiceException ("Service " + Name + " contains duplicate procedures " + signature.Name);
            Procedures [signature.Name] = signature;
        }

        /// <summary>
        /// Add a procedure to the service for the given method annotated with the KRPCProcedure attribute.
        /// </summary>
        public void AddProcedure (MethodInfo method)
        {
            TypeUtils.ValidateKRPCProcedure (method);
            AddProcedure (new ProcedureSignature (
                Name, method.Name, method.GetDocumentation (),
                new ProcedureHandler (method, TypeUtils.GetNullable (method)), GameScene));
        }

        /// <summary>
        /// Add a property to the service for the given property annotated with the KRPCProperty attribute.
        /// </summary>
        public void AddProperty (PropertyInfo property)
        {
            TypeUtils.ValidateKRPCProperty (property);
            var getter = property.GetGetMethod ();
            var setter = property.GetSetMethod ();
            if (getter != null)
                AddPropertyProcedure (property, getter);
            if (setter != null)
                AddPropertyProcedure (property, setter);
        }

        void AddPropertyProcedure (MemberInfo property, MethodInfo method)
        {
            var handler = new ProcedureHandler (method, TypeUtils.GetNullable (property));
            AddProcedure (new ProcedureSignature (Name, method.Name, property.GetDocumentation (), handler, GameScene));
        }

        /// <summary>
        /// Add a class to the service for the given class type annotated with the KRPCClass attribute.
        /// Returns the name of the class.
        /// </summary>
        public string AddClass (Type classType)
        {
            TypeUtils.ValidateKRPCClass (classType);
            var name = classType.Name;
            if (Classes.ContainsKey (name))
                throw new ServiceException ("Service " + Name + " contains duplicate classes " + name);
            Classes [name] = new ClassSignature (Name, name, classType.GetDocumentation ());
            return name;
        }

        /// <summary>
        /// Add an enum to the service for the given enum type annotated with the KRPCEnum attribute.
        /// Returns the name of the enumeration.
        /// </summary>
        public string AddEnum (Type enumType)
        {
            TypeUtils.ValidateKRPCEnum (enumType);
            var name = enumType.Name;
            if (Enumerations.ContainsKey (name))
                throw new ServiceException ("Service " + Name + " contains duplicate enumerations " + name);
            var values = new List<EnumerationValueSignature> ();
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                values.Add (new EnumerationValueSignature (Name, name, field.Name, (int)field.GetRawConstantValue (), field.GetDocumentation ()));
            }
            Enumerations [name] = new EnumerationSignature (Name, name, values, enumType.GetDocumentation ());
            return name;
        }

        /// <summary>
        /// Add an exception to the service for the given exception type annotated with the KRPCException attribute.
        /// Returns the name of the exception.
        /// </summary>
        public string AddException (Type exnType)
        {
            TypeUtils.ValidateKRPCException (exnType);
            var name = exnType.Name;
            if (Exceptions.ContainsKey (name))
                throw new ServiceException ("Service " + Name + " contains duplicate exceptions " + name);
            Exceptions [name] = new ExceptionSignature (Name, name, exnType.GetDocumentation ());
            return name;
        }

        /// <summary>
        /// Add a class method to the given class in the given service for the given class type annotated with the KRPCClass attribute.
        /// </summary>
        public void AddClassMethod (string cls, Type classType, MethodInfo method)
        {
            TypeUtils.ValidateIdentifier (cls);
            TypeUtils.ValidateKRPCMethod (classType, method);
            if (!Classes.ContainsKey (cls))
                throw new ArgumentException ("Class " + cls + " does not exist");
            var name = method.Name;
            if (!method.IsStatic) {
                var handler = new ClassMethodHandler (classType, method, TypeUtils.GetNullable(method));
                AddProcedure (new ProcedureSignature (Name, cls + '_' + name, method.GetDocumentation (), handler, GameScene));
            } else {
                var handler = new ClassStaticMethodHandler (method, TypeUtils.GetNullable (method));
                AddProcedure (new ProcedureSignature (Name, cls + "_static_" + name, method.GetDocumentation (), handler, GameScene));
            }
        }

        /// <summary>
        /// Add a class property to the given class in the given service for the given property annotated with the KRPCProperty attribute.
        /// </summary>
        public void AddClassProperty (string cls, Type classType, PropertyInfo property)
        {
            TypeUtils.ValidateIdentifier (cls);
            TypeUtils.ValidateKRPCClassProperty (classType, property);
            if (!Classes.ContainsKey (cls))
                throw new ArgumentException ("Class " + cls + " does not exist");
            var getter = property.GetGetMethod ();
            var setter = property.GetSetMethod ();
            if (getter != null)
                AddClassPropertyMethod (cls, classType, property, getter, TypeUtils.GetNullable (property));
            if (setter != null)
                AddClassPropertyMethod (cls, classType, property, setter, false);
        }

        void AddClassPropertyMethod (string cls, Type classType, MemberInfo property, MethodInfo method, bool nullable)
        {
            var handler = new ClassMethodHandler (classType, method, nullable);
            AddProcedure (new ProcedureSignature (Name, cls + '_' + method.Name, property.GetDocumentation (), handler, GameScene));
        }

        public void GetObjectData (SerializationInfo info, StreamingContext context)
        {
            info.AddValue ("documentation", Documentation);
            info.AddValue ("procedures", Procedures);
            info.AddValue ("classes", Classes);
            info.AddValue ("enumerations", Enumerations);
            info.AddValue ("exceptions", Exceptions);
        }
    }
}
