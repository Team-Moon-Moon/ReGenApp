using UnityEngine;
using System.Collections;
using System;
using System.IO;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Linq;

public class UITest
{
    const float WaitTimeout = 2;
    const float WaitIntervalFrames = 10;
    static MonoBehaviour mb;

    public class MB : MonoBehaviour
    {
    }

    protected void CreateMonoBehaviour()
    {
        if (mb == null)
        {
            var go = new GameObject("mb");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            mb = go.AddComponent<MB>();            
        }
    }

    protected Coroutine WaitFor(Condition condition)
    {
        CreateMonoBehaviour();        
        return mb.StartCoroutine(WaitForInternal(condition, Environment.StackTrace));
    }
                
    protected Coroutine LoadScene(string name)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(LoadSceneInternal(name));
    }

    IEnumerator LoadSceneInternal(string name)
    {           
#if UNITY_EDITOR
        if (name.Contains(".unity"))
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(name, new LoadSceneParameters());
            yield return WaitFor(new SceneLoaded(Path.GetFileNameWithoutExtension(name)));
            yield break;
        }
#endif        
        SceneManager.LoadScene(name);
        yield return WaitFor(new SceneLoaded(name));
    }

    protected Coroutine AssertLabel(string id, string text)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(AssertLabelInternal(id, text));
    }

    protected Coroutine Press(string buttonName)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(PressInternal(buttonName));
    }

    protected Coroutine Press(GameObject o)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(PressInternal(o));
    }

    protected Coroutine TypeInto(string inputName, string text)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(TypeIntoInternal(inputName, text));
    }

    protected Coroutine TypeInto(string inputName, int idx, string text)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(TypeIntoInternal(inputName, idx, text));
    }

    protected Coroutine TypeInto(GameObject o, string text)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(TypeIntoInternal(o, text));
    }

    IEnumerator WaitForInternal(Condition condition, string stackTrace)
    {
        float time = 0;
        while (!condition.Satisfied())
        {
            if (time > WaitTimeout)
            { 
                Debug.LogException(new Exception("Operation timed out: " + condition + "\n" + stackTrace));
                yield break;
            }
            for (int i = 0; i < WaitIntervalFrames; i++) {
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    IEnumerator PressInternal(string buttonName)
    {
        var buttonAppeared = new ObjectAppeared(buttonName);
        yield return WaitFor(buttonAppeared);
        yield return Press(buttonAppeared.o);
    }

    IEnumerator PressInternal(GameObject o)
    {
        yield return WaitFor(new ButtonAccessible(o));
        Debug.Log("Button pressed: " + o);
        ExecuteEvents.Execute(o, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        yield return null;
    }

    IEnumerator TypeIntoInternal(string inputName, int idx, string text)
    {
        var buttonAppeared = new ObjectAppeared(inputName, idx);
        yield return WaitFor(buttonAppeared);
        yield return TypeIntoInternal(buttonAppeared.o, text);
    }

    IEnumerator TypeIntoInternal(string inputName, string text)
    {
        var buttonAppeared = new ObjectAppeared(inputName);
        yield return WaitFor(buttonAppeared);
        yield return TypeIntoInternal(buttonAppeared.o, text);
    }


    IEnumerator TypeIntoInternal(GameObject o, string text)
    {
        yield return WaitFor(new InputFieldAccessible(o));
        Debug.Log("Input Field \"" + o + "\" Typed: " + text);
        var input = o.GetComponent<InputField>();
        input.Select();
        input.ActivateInputField();
        input.text = text;
        input.onEndEdit.Invoke(text);
        yield return null;
    }

    IEnumerator AssertLabelInternal(string id, string text)
    {
        yield return WaitFor(new LabelTextAppeared(id, text));
    }
                
    protected abstract class Condition
    {
        protected string param;
        protected string objectName;

        public Condition()
        {
        }

        public Condition(string param)
        {
            this.param = param;
        }

        public Condition(string objectName, string param)
        {
            this.param = param;
            this.objectName = objectName;
        }

        public abstract bool Satisfied();

        public override string ToString()
        {
            return GetType() + " '" + param + "'";
        }
    }

    protected class LabelTextAppeared : Condition
    {
        public LabelTextAppeared(string objectName, string param) : base(objectName, param) {}

        public override bool Satisfied()
        {
            return GetErrorMessage() == null;
        }

        string GetErrorMessage()
        {
            var go = GameObject.Find(objectName);
            if (go == null) return "Label object " + objectName + " does not exist";
            if (!go.activeInHierarchy) return "Label object " + objectName + " is inactive";
            var t = go.GetComponent<Text>();
            if (t == null) return "Label object " + objectName + " has no Text attached";
            if (t.text != param) return "Label " + objectName + "\n text expected: " + param + ",\n actual: " + t.text;
            return null;
        }

        public override string ToString()
        {
            return GetErrorMessage();
        }
    }

    protected class SceneLoaded : Condition
    {        
        public SceneLoaded(string param) : base (param) {}

        public override bool Satisfied()
        {
            return SceneManager.GetActiveScene().name == param;
        }
    }

    protected class ObjectAnimationPlaying : Condition
    {
        public ObjectAnimationPlaying(string objectName, string param) :base (objectName, param) {}

        public override bool Satisfied()
        {
            GameObject gameObject = GameObject.Find(objectName);
            return gameObject.GetComponent<Animation>().IsPlaying(param);
        }
    }

    protected class ObjectAppeared<T> : Condition where T : Component
    {
        protected int idx;

        public ObjectAppeared(int idx = 0) {
            this.idx = idx;
        }

        public override bool Satisfied()
        {
            if (idx == 0)
            {
                var obj = UnityEngine.Object.FindObjectOfType(typeof(T)) as T;
                return obj != null && obj.gameObject.activeInHierarchy;
            } else
            {
                var objects = UnityEngine.Object.FindObjectsOfType(typeof(T)).Where(o => (o as Component).gameObject.activeInHierarchy).Count();
                return objects < idx;
            }
        }
    }

    protected class ObjectDisappeared<T> : Condition where T : Component
    {
        public override bool Satisfied()
        {
            var obj = GameObject.FindObjectOfType(typeof(T)) as T;
            return obj == null || !obj.gameObject.activeInHierarchy;
        }
    }

    protected class ObjectAppeared : Condition
    {
        protected string path;
        protected int idx;
        public GameObject o;

        public ObjectAppeared(string path, int idx = 0)
        {
            this.path = path;
            this.idx = idx;
        }

        public override bool Satisfied()
        {
            if (idx == 0)
            {
                o = GameObject.Find(path);
                return o != null && o.activeInHierarchy;
            } else
            {
                var objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                int i = 0;
                foreach(var obj in objects)
                {
                    if (UnityEditor.AnimationUtility.CalculateTransformPath(obj.transform, obj.transform.root).Contains(path) && obj.activeInHierarchy)
                    {
                        if (i == idx)
                        {
                            o = obj;
                            return true;
                        }
                        else
                            i++;
                    }
                }
                return false;
            }
        }

        public override string ToString()
        {
            return "ObjectAppeared(" + path + ")";
        }
    }

    protected class ObjectDisappeared : ObjectAppeared
    {
        public ObjectDisappeared(string path) : base(path) {}

        public override bool Satisfied()
        {
            return !base.Satisfied();
        }

        public override string ToString()
        {
            return "ObjectDisappeared(" + path + ")";
        }
    }

    protected class BoolCondition : Condition
    {
        private Func<bool> _getter;

        public BoolCondition(Func<bool> getter)
        {
            _getter = getter;
        }

        public override bool Satisfied()
        {
            if (_getter == null) return false;
            return _getter();
        }

        public override string ToString()
        {
            return "BoolCondition(" + _getter + ")";
        }
    }

    protected class ButtonAccessible : Condition
    {
        GameObject button;

        public ButtonAccessible(GameObject button)
        {
            this.button = button;
        }

        public override bool Satisfied()
        {
            return GetAccessibilityMessage() == null;
        }

        public override string ToString()
        {
            return GetAccessibilityMessage() ?? "Button " + button.name + " is accessible";
        }

        string GetAccessibilityMessage()
        {
            if (button == null)
                return "Button " + button + " not found";
            if (button.GetComponent<Button>() == null && button.GetComponent<Toggle>() == null)
                return "GameObject " + button + " does not have a Button or Toggle component attached";
            return null;
        }
    }

    protected class InputFieldAccessible : Condition
    {
        GameObject button;

        public InputFieldAccessible(GameObject button)
        {
            this.button = button;
        }

        public override bool Satisfied()
        {
            return GetAccessibilityMessage() == null;
        }

        public override string ToString()
        {
            return GetAccessibilityMessage() ?? "Button " + button.name + " is accessible";
        }

        string GetAccessibilityMessage()
        {
            if (button == null)
                return "Button " + button + " not found";
            if (button.GetComponent<InputField>() == null)
                return "GameObject " + button + " does not have a InputField component attached";
            return null;
        }
    }
}
