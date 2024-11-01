using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace CommandStructure
{
    /// <summary>
    /// The 'Command' abstract class
    /// </summary>

    [Serializable]
    public class Command
    {
        protected Tracer tracer;
        [SerializeField] public string commandType;

        // Constructor
        public Command()
        {
            
        }
        public Command(Tracer tracer)
        {
            this.tracer = tracer;
        }

        public virtual void Execute() { }

        public virtual void UnExecute() { }
        public virtual void Impact() { }

        public virtual void Save(int index)
        {
            tracer.Save(index);
        }

        public void SetTracer(Tracer tracer)
        {
            this.tracer = tracer;
        }
    }

    enum OperationType
    {

    }

    [Serializable]
    class AdjustCommand : Command
    {
        [SerializeField] public List<uint> _indexes;
        [SerializeField] public int _intensity;
        [SerializeField] public int _type;
        // Constructor
        public AdjustCommand()
        {
        }

        /// <summary>
        /// adjust a range of voxels
        /// </summary>
        /// <param name="tracer">tracer</param>
        /// <param name="indexes">the indexes of target voxel to be adjusted</param>
        /// <param name="intensity">the offset of intensity</param>
        public AdjustCommand(Tracer tracer, List<uint> indexes, int intensity) : base(tracer)
        {
            _indexes = indexes;
            _intensity = intensity;
            _type = 2;
            commandType = "Adjust";
        }

        /// <summary>
        /// adjust a branch from the target to the trunk
        /// </summary>
        /// <param name="tracer"></param>
        /// <param name="index">the target point on a branch</param>
        public AdjustCommand(Tracer tracer, uint index) : base(tracer)
        {
            _indexes = tracer.GetBranch(index);
            _intensity = 0;
            _type = tracer.Confidence(_indexes) > 0.6f ? 2 : 4;
            Debug.Log($"branch confidence:{tracer.Confidence(_indexes)}");
            Debug.Log(_indexes.Count);
            commandType = "Adjust";
        }

        public override void Execute()
        {
            tracer.AdjustIntensity(_indexes, _intensity, false);
            tracer.TraceBranch(_type);
        }

        public override void UnExecute()
        {
            tracer.AdjustIntensity(_indexes, _intensity, true);
            tracer.Trace(_type);
        }

        public override void Impact()
        {
            tracer.AdjustIntensity(_indexes, _intensity, false);
        }

        public override string ToString()
        {
            return $"{commandType} Command indexes count: {_indexes.Count} intensity: {_intensity}";
        }
    }

    [Serializable]
    class MaskCommand : Command
    {
        [SerializeField] public List<uint> _indexes;
        // Constructor

        public MaskCommand() 
        {
        }
        public MaskCommand(Tracer tracer, List<uint> indexes) : base(tracer)
        {
            _indexes = indexes;
            commandType = "Mask";
        }
        public override void Execute()
        {
            tracer.ModifyMask(_indexes, false, 4);
        }

        public override void UnExecute()
        {
            tracer.ModifyMask(_indexes, true, 2);
        }

        public override void Impact()
        {
            tracer.ModifyMask(_indexes, false, 0);
        }

        public override string ToString()
        {
            return $"{commandType} Command indexes count: {_indexes.Count}";
        }
    }

    [Serializable]
    class DeleteCommand : Command
    {
        public List<uint> _indexes;

        public DeleteCommand ()
        {
        }
        public DeleteCommand(Tracer tracer, uint index) : base(tracer)
        {
            _indexes = tracer.GetCluster(index);
            Debug.Log(_indexes.Count);
            commandType = "Delete";
        }

        public override void Execute()
        {
            tracer.ModifyMask(_indexes, false, 4);
        }

        public override void UnExecute()
        {
            tracer.ModifyMask(_indexes, true, 4);
        }
    }

    [Serializable]
    class AutoCommand : Command
    {
        [SerializeField] public int bkgThreshold;
        [SerializeField] public float somaRadius;
        [SerializeField] public Vector3Int rootPos;
        public AutoCommand() 
        {
        }
        public AutoCommand(Tracer tracer, int bkgThreshold, float somaRadius, Vector3Int rootPos) : base(tracer)
        {
            this.bkgThreshold = bkgThreshold;
            this.somaRadius = somaRadius;
            this.rootPos = rootPos;
            commandType = "Auto";
        }

        public override void Execute()
        {
            tracer.Initial(bkgThreshold, somaRadius, rootPos);
            tracer.Trace(3);
            //tracer.FMM();
        }

        public override void UnExecute()
        {

        }

        public override void Impact()
        {
            tracer.Initial(bkgThreshold, somaRadius, rootPos);
        }

        public override string ToString()
        {
            return $"{commandType} Command： bkgThreshold:{bkgThreshold}, somaRadius:{somaRadius}, rootPos:{rootPos}";
        }
    }

    class ThreshCommand: Command
    {
        public float hitPosX;
        public float hitPosY;
        public float hitPosZ;
        public float directionX;
        public float directionY;
        public float directionZ;

        public ThreshCommand() { }
        public ThreshCommand(Tracer tracer,Vector3 hitPos, Vector3 direction):base(tracer)
        {
            this.hitPosX = hitPos.x;
            this.hitPosY = hitPos.y;
            this.hitPosZ = hitPos.z;
            this.directionX = direction.x;
            this.directionY = direction.y;
            this.directionZ = direction.z;
            this.commandType = "Thresh";
        }

        public override void Execute()
        {
            Vector3 hitPos = new Vector3(hitPosX, hitPosY, hitPosZ);
            Vector3 direction = new Vector3(directionX, directionY, directionZ);
            tracer.AdjustThreshold(hitPos, direction);
        }

        public override void UnExecute()
        {

        }

        public override void Impact()
        {
            Vector3 hitPos = new Vector3(hitPosX, hitPosY, hitPosZ);
            Vector3 direction = new Vector3(directionX, directionY, directionZ);
            tracer.AdjustThreshold(hitPos, direction);
        }

        public override string ToString()
        {
            Vector3 hitPos = new Vector3(hitPosX, hitPosY, hitPosZ);
            Vector3 direction = new Vector3(directionX, directionY, directionZ);
            return $"{commandType} Command： hitPos:{hitPos}, direction:{direction}";
        }

        public override void Save(int index)
        {

        }
    }
    /// <summary>
    /// The 'Invoker' class
    /// </summary>
    [Serializable]
    public class CMDInvoker : MonoBehaviour
    {
        [SerializeField] private List<Command> _commands = new();
        [SerializeField] private int _current = -1;
        public string savePath;
        public string loadPath;
        public int step;
        public Tracer tracer;
        int commandOffset = 0;

        public void Redo()
        {
            if (_current < _commands.Count - 1)
            {
                Command command = _commands[++_current];
                command.Execute();

                Debug.Log("Redo:" + command.ToString());
            }
        }

        public void Undo()
        {
            if (_current >= 0)
            {
                Command command = _commands[_current--];
                command.UnExecute();

                Debug.Log("Undo:" + command.ToString());
            }
        }

        public void Execute(Command command)
        {
            if (command is DeleteCommand && LastCammond() is DeleteCommand) { return; }
            if (command is AutoCommand) commandOffset = _current + 1;
            command.Execute();
            if (++_current >= _commands.Count)
            {
                _commands.Add(command);
            }
            else
            {
                _commands[_current] = command;
                if (_current < _commands.Count - 1)
                {
                    _commands.RemoveRange(_current + 1, _commands.Count - _current - 1);
                }
            }

            command.Save(_current-commandOffset);
            Debug.Log($"current command id:{_current} Excute:" + command.ToString());

            //Save();
        }

        public Command LastCammond()
        {
            return _commands[_current];
        }

        public void Clear()
        {
            _commands = new();
            _current = -1;
        }

        [InspectorButton]
        public void Save()
        {
            string jsonStr = JsonConvert.SerializeObject(_commands.GetRange(0,_current+1));
            //Debug.Log("json：" + jsonStr);
            File.WriteAllText(savePath, jsonStr);
        }

        [InspectorButton]
        public void Load()
        {
            string jsonStr = File.ReadAllText(loadPath);
            _commands = JsonConvert.DeserializeObject<List<Command>>(jsonStr, new CommandConverter());
        }

        [InspectorButton]
        public void Reproduce()
        {
            for (int i = 0; i < step; i++)
            {
                Command command = _commands[i];
                command.SetTracer(tracer);
                command.Impact();
            }
            Command curCommand = _commands[step];
            curCommand.SetTracer(tracer);
            curCommand.Execute();
            _current = step;
        }

    }
    public class CommandConverter : JsonCreationConvertor<Command>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        protected override Command Create(Type objectType, JObject jObject)
        {
            string commandType = (string)jObject["commandType"];
            switch (commandType)
            { 
                case "Adjust":
                    {
                        return new AdjustCommand();
                    }
                case "Mask":
                    {
                        return new MaskCommand();
                    }
                case "Auto":
                    {
                        return new AutoCommand();
                    }
                case "Thresh":
                    {
                        return new ThreshCommand();
                    }
                default:
                    return new Command();
            }
        }

        private bool FieldExists(string fieldName, JObject jObject)
        {
            return jObject[fieldName] != null;
        }
    }

    public abstract class JsonCreationConvertor<T> : JsonConverter
    {
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType)
        {
            return typeof(T).IsAssignableFrom(objectType);
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            T target = Create(objectType, jObject);

            serializer.Populate(jObject.CreateReader(), target);

            return target;
        }
    }
}


