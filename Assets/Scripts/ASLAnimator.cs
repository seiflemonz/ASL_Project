using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class ASLAnimator : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Animator _animator;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private float _crossfadeTime = 0.15f;

    private struct ASLAction
    {
        public AnimationClip Clip;
        public string Text;
    }

    private PlayableGraph _playableGraph;
    private AnimationPlayableOutput _playableOutput;
    private AnimationMixerPlayable _mixer;
    private Coroutine _playCoroutine;
    private Coroutine _crossfadeRoutine;

    private void Awake()
    {
        InitializeGraph();
        SetStatusActive(false);
    }

    private void OnDestroy()
    {
        if (_playableGraph.IsValid())
        {
            _playableGraph.Destroy();
        }
    }

    private void InitializeGraph()
    {
        _playableGraph = PlayableGraph.Create("ASLGraph");
        _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        
        _mixer = AnimationMixerPlayable.Create(_playableGraph, 2);
        _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);
        _playableOutput.SetSourcePlayable(_mixer);
        
        ReturnToEntryState();
        _playableGraph.Play();
    }

    public void PlayText()
    {
        if (_inputField == null) return;
        
        string text = _inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
        }
        
        var actions = GetSequenceActions(text);
        _playCoroutine = StartCoroutine(PlaySequence(actions));
    }

    private void ReturnToEntryState()
    {
        if (_animator.runtimeAnimatorController == null) return;
        
        var controller = AnimatorControllerPlayable.Create(_playableGraph, _animator.runtimeAnimatorController);
        StartCrossfade(controller);
    }

    private void StartCrossfade(Playable nextPlayable)
    {
        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
            CleanUpMixer();
        }
        
        _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(nextPlayable));
    }

    private IEnumerator CrossfadeRoutine(Playable next)
    {
        _mixer.ConnectInput(1, next, 0);
        
        float duration = Mathf.Max(0.001f, _crossfadeTime);
        float t = 0f;
        
        while (t < duration)
        {
            t += Time.deltaTime;
            float weight = t / duration;
            _mixer.SetInputWeight(0, 1f - weight);
            _mixer.SetInputWeight(1, weight);
            yield return null;
        }

        CleanUpMixer();
        _crossfadeRoutine = null;
    }

    private void CleanUpMixer()
    {
        Playable port1 = _mixer.GetInput(1);
        if (!port1.IsValid()) return;
        
        Playable port0 = _mixer.GetInput(0);
        _mixer.DisconnectInput(0);
        _mixer.DisconnectInput(1);
        
        if (port0.IsValid()) port0.Destroy();
        
        _mixer.ConnectInput(0, port1, 0);
        _mixer.SetInputWeight(0, 1f);
        _mixer.SetInputWeight(1, 0f);
    }

    private IEnumerator PlaySequence(List<ASLAction> actions)
    {
        SetStatusActive(true);

        foreach (var action in actions)
        {
            UpdateStatusText(action.Text);
            yield return PlayClip(action.Clip);
        }

        SetStatusActive(false);
        ReturnToEntryState();
    }

    private List<ASLAction> GetSequenceActions(string text)
    {
        var actions = new List<ASLAction>();
        string[] words = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        int i = 0;

        while (i < words.Length)
        {
            int matchCount = TryMatchPhrase(words, i, out AnimationClip clip);
            
            if (matchCount > 0) 
            {
                actions.Add(new ASLAction { Clip = clip, Text = string.Join(" ", words, i, matchCount) });
                i += matchCount;
                continue;
            }
            
            AddSpellWordActions(words[i], actions);
            i++;
        }

        return actions;
    }

    private int TryMatchPhrase(string[] words, int startIndex, out AnimationClip clip)
    {
        for (int len = words.Length - startIndex; len > 0; len--)
        {
            string phrase = string.Join(" ", words, startIndex, len);
            clip = LoadClip("Words/" + phrase.ToLower());
            
            if (clip != null) return len;
        }
        
        clip = null;
        return 0;
    }

    private void AddSpellWordActions(string word, List<ASLAction> actions)
    {
        foreach (char c in word)
        {
            AnimationClip clip = GetCharacterClip(c);
            if (clip != null)
            {
                actions.Add(new ASLAction { Clip = clip, Text = c.ToString() });
            }
        }
    }

    private AnimationClip GetCharacterClip(char c)
    {
        if (char.IsDigit(c))
        {
            return LoadClip("Numbers/number_" + c);
        }
        
        if (char.IsLetter(c))
        {
            return LoadClip("Letters/letter_" + char.ToUpper(c));
        }
        
        return null;
    }

    private AnimationClip LoadClip(string path)
    {
#if UNITY_EDITOR
        string fullPath = "Assets/Animations/SGL/" + path + ".fbx";
        AnimationClip clip = ExtractClip(fullPath);
        
        if (clip != null) return clip;
#endif
        return Resources.Load<AnimationClip>("SGL/" + path);
    }

    private AnimationClip ExtractClip(string path)
    {
#if UNITY_EDITOR
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
#endif
        return null;
    }

    private IEnumerator PlayClip(AnimationClip clip)
    {
        var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
        clipPlayable.Play();
        
        StartCrossfade(clipPlayable);
        
        yield return new WaitForSeconds(Mathf.Max(0f, clip.length - _crossfadeTime));
    }

    private void SetStatusActive(bool isActive)
    {
        if (_statusText != null)
        {
            _statusText.gameObject.SetActive(isActive);
        }
    }

    private void UpdateStatusText(string text)
    {
        if (_statusText != null)
        {
            _statusText.text = text;
        }
    }
}
