using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using rng = UnityEngine.Random;

public class BamDirButtonScript : MonoBehaviour
{
    [SerializeField]
    private MeshRenderer _buttonRenderer, _upRenderer, _downRenderer;
    [SerializeField]
    private TextMesh _buttonText, _upText, _downText;
    [SerializeField]
    private MeshRenderer[] _LEDs;
    [SerializeField]
    private Light[] _LEDLights;
    [SerializeField]
    private KMRuleSeedable _ruleSeed;
    [SerializeField]
    private KMBombInfo _info;
    [SerializeField]
    private KMSelectable _upSel, _downSel, _buttonSel;
    [SerializeField]
    private KMAudio _audio;
    [SerializeField]
    private KMBombModule _module;

    private static int _idc;
    private int _id = ++_idc, _upColor, _downColor, _buttonColor, _requiredPresses, _presses, _stagesDone, _stagesRequired = 1;
    private List<Func<bool>> _rules, _releaseRules;
    private Func<bool> _rule, _releaseRule;
    private int[][] _numsTable;
    private bool _strikeLeeway, _isSolved;
    private float _holdTime;

    private static readonly List<Color> _colors = new List<Color>() { Color.black, Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.white };
    private static readonly List<string> _colorNames = new List<string>() { "Black", "Red", "Green", "Blue", "Cyan", "Magenta", "Yellow", "White" };
    private static readonly char[] ALPHA = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    private int TimerDigit(int place)
    {
        if(place == 0)
            return (int)_info.GetTime() % 10;
        if(place == 1)
            return ((int)_info.GetTime() % 60) / 10;
        return ((int)_info.GetTime() / 60 * (int)Math.Pow(10, place - 2)) % 10;
    }

    private bool IsPrime(int n)
    {
        return new int[] { 2, 3, 5, 7 }.Contains(n);
    }

    void Start()
    {
        DoRuleSeed();
        foreach(Light l in _LEDLights)
        {
            l.range *= transform.lossyScale.y;
            l.enabled = false;
        }
        foreach(MeshRenderer l in _LEDs)
            l.material.color = new Color(0f, 0f, 0f);

        GenerateArrows();
        GenerateButton();

        _upSel.OnInteract += () => { PressUp(); return false; };
        _downSel.OnInteract += () => { PressDown(); return false; };
        _buttonSel.OnInteract += () => { Press(); return false; };
        _buttonSel.OnInteractEnded += () => { Release(); };
    }

    private void PressDown()
    {
        _audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, _downSel.transform);
        _downSel.AddInteractionPunch(0.5f);
        if(_isSolved)
            return;

        if(_presses == _requiredPresses && _presses % 2 == 1)
        {
            Debug.LogFormat("[Bamboozling Directional Button #{0}] Next stage!", _id);
            NextStage();
        }
        else
        {
            Strike("Bad down press.");
        }
        GenerateArrows();
    }

    private void PressUp()
    {
        _audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, _upSel.transform);
        _upSel.AddInteractionPunch(0.5f);
        if(_isSolved)
            return;

        if(_presses == _requiredPresses && _presses % 2 == 0)
        {
            Debug.LogFormat("[Bamboozling Directional Button #{0}] Next stage!", _id);
            NextStage();
        }
        else
        {
            Strike("Bad up press.");
        }
        GenerateArrows();
    }

    private void NextStage()
    {
        _stagesDone++;
        _presses = 0;
        for(int i = 0; i < 3; i++)
        {
            _LEDs[i].material.color = Color.green;
            _LEDLights[i].enabled = true;
            _LEDLights[i].color = Color.green;
        }
        if(_stagesDone >= _stagesRequired)
        {
            StartCoroutine(Solve());
            return;
        }
        _audio.PlaySoundAtTransform("Stage", transform);
    }

    private IEnumerator Solve()
    {
        _audio.PlaySoundAtTransform("Solve", transform);

        _isSolved = true;
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Module solved!", _id);
        _module.HandlePass();
        yield break;
    }

    private void Press()
    {
        _buttonSel.AddInteractionPunch(1f);
        _audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, _buttonSel.transform);
        if(_isSolved)
            return;

        _holdTime = Time.time;

        _strikeLeeway = false;
        if(_rule())
        {
            _presses++;
            Debug.LogFormat("[Bamboozling Directional Button #{0}] Good button press!", _id);

            for(int i = _stagesDone; i < _LEDs.Length; i++)
            {
                _LEDs[i].material.color = Color.gray;
                _LEDLights[i].color = Color.gray;
                _LEDLights[i].enabled = true;
            }
        }
        else
            Strike("Bad button press. (Time = " + (int)_info.GetTime() + ")");
    }

    private void Release()
    {
        _audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, _buttonSel.transform);
        if(_isSolved)
            return;

        if(!_releaseRule() || Time.time - _holdTime < 2f)
            Strike("Bad button release. (Time = " + (int)_info.GetTime() + ")");
        GenerateButton();
    }

    private void Strike(string message = "")
    {
        if(_strikeLeeway)
            return;

        Debug.LogFormat("[Bamboozling Directional Button #{0}] {1}", _id, message);
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Strike! Resetting...", _id);
        _module.HandleStrike();

        StartCoroutine(AnimateStrike());
        _presses = 0;
        _stagesDone = 0;
        _stagesRequired = 1;
        _strikeLeeway = true;
    }

    private IEnumerator AnimateStrike()
    {
        foreach(MeshRenderer l in _LEDs)
            l.material.color = Color.red;
        foreach(Light l in _LEDLights)
        {
            l.color = Color.red;
            l.enabled = true;
        }
        yield return new WaitForSeconds(1f);
        foreach(MeshRenderer l in _LEDs)
            l.material.color = Color.black;
        foreach(Light l in _LEDLights)
            l.enabled = false;
    }

    private void DoRuleSeed()
    {
        MonoRandom rng = _ruleSeed.GetRNG();
        int[] order = Enumerable.Repeat(new int[] { 3, 4, 5, 6 }, 16).SelectMany(i => i).OrderBy(item => rng.NextDouble()).ToArray();

        _numsTable = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }.Select(i => new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }.Select(j => order[8 * j + i]).ToArray()).ToArray();

        int _1 = rng.Next(10), _2 = rng.Next(5) + 6, _3 = rng.Next(10), _4 = rng.Next(5) + 6;
        List<Func<bool>> _allRules = new List<Func<bool>> {
            () => true,
            () => TimerDigit(0) == _1,
            () => TimerDigit(0) == _info.GetSolvableModuleNames().Count - _info.GetSolvedModuleNames().Count,
            () => TimerDigit(0) == _info.GetSolvedModuleIDs().Count,
            () => TimerDigit(0) == TimerDigit(2),
            () => TimerDigit(0) + TimerDigit(1) == _2,
            () => IsPrime(TimerDigit(0)) && IsPrime(TimerDigit(1)),
            () => true,
            () => true,
            () => true,
            () => true,
            () => true,
            () => true
        };
        List<Func<bool>> _allReleaseRules = new List<Func<bool>> {
            () => true,
            () => true,
            () => true,
            () => true,
            () => true,
            () => true,
            () => true,
            () => TimerDigit(0) == _3,
            () => TimerDigit(0) == _info.GetSolvableModuleNames().Count - _info.GetSolvedModuleNames().Count,
            () => TimerDigit(0) == _info.GetSolvedModuleIDs().Count,
            () => TimerDigit(0) == TimerDigit(2),
            () => TimerDigit(0) + TimerDigit(1) == _4,
            () => IsPrime(TimerDigit(0)) && IsPrime(TimerDigit(1))
        };
        int[] ixr = Enumerable.Range(0, 13).OrderBy(item => rng.NextDouble()).ToArray();
        _rules = Enumerable.Range(0, 8).Select(i => _allRules[ixr[i]]).ToList();
        _releaseRules = Enumerable.Range(0, 8).Select(i => _allReleaseRules[ixr[i]]).ToList();
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Using Rule Seed {1}.", _id, rng.Seed);
        Debug.LogFormat("<Bamboozling Directional Button #{0}> order = {1}", _id, order.Join(""));
        Debug.LogFormat("<Bamboozling Directional Button #{0}> ixr = {1}", _id, ixr.Join(" "));
        Debug.LogFormat("<Bamboozling Directional Button #{0}> _1 _2 _3 _4 = {1} {2} {3} {4}", _id, _1, _2, _3, _4);
    }

    private void GenerateArrows()
    {
        _upColor = rng.Range(0, 8);
        _downColor = rng.Range(0, 8);
        _upRenderer.material.color = _colors[_upColor];
        _upText.color = _colors[_upColor].Inverse();
        _downRenderer.material.color = _colors[_downColor];
        _downText.color = _colors[_downColor].Inverse();

        _requiredPresses = _numsTable[_downColor][_upColor];
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Arrow colors are: Up: {1} Down: {2}", _id, _colorNames[_upColor], _colorNames[_downColor]);
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Required presses: {1}", _id, _requiredPresses);
    }

    private void GenerateButton()
    {
        _buttonColor = rng.Range(0, 8);
        _buttonRenderer.material.color = _colors[_buttonColor];
        _buttonText.color = _colors[_buttonColor].Inverse();

        string str = ALPHA.PickRandom().ToString() + ALPHA.PickRandom().ToString() + ALPHA.PickRandom().ToString();
        _buttonText.text = str;
        int t = 1;
        foreach(char c in str)
            t *= c - 'A' + 2;
        t += _buttonColor;
        t %= _rules.Count;
        _rule = _rules[t];
        _releaseRule = _releaseRules[t];
        Debug.LogFormat("[Bamboozling Directional Button #{0}] Button color is: {1} Label: {2}", _id, _colorNames[_buttonColor], str);
        Debug.LogFormat("<Bamboozling Directional Button #{0}> rule = {1}", _id, t);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use ""!{0} press ## ## ##"" to press the button at any of those times. Use ""!{0} release ## ## ##"" to release the button at any of those times. Use ""!{0} press up"" or ""!{0} press down"" to press that arrow button. Use ""!{0} uncap"" to break your soul.";
#pragma warning restore 414

    private bool _held, _uncapCheck;

    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;

        if(!_held && !_uncapCheck && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*press((?: +[0-5]\d)*)\s*$")).Success)
        {
            string[] inputs = m.Groups[1].Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] times = inputs.Select(s => int.Parse(s)).ToArray();
            if(times.Length == 0)
            {
                yield return null;
                _buttonSel.OnInteract();
                _held = true;
                yield break;
            }
            yield return null;
            while(!times.Any(t => (int)_info.GetTime() % 60 == t))
                yield return "trycancel";
            _buttonSel.OnInteract();
            _held = true;
            yield break;
        }

        if(_held && !_uncapCheck && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*release((?: +[0-5]\d)*)\s*$")).Success)
        {
            string[] inputs = m.Groups[1].Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] times = inputs.Select(s => int.Parse(s)).ToArray();
            if(times.Length == 0)
            {
                yield return null;
                _buttonSel.OnInteractEnded();
                _held = false;
                yield break;
            }
            yield return null;
            while(!times.Any(t => (int)_info.GetTime() % 60 == t))
                yield return "trycancel";
            _buttonSel.OnInteractEnded();
            _held = false;
            yield break;
        }

        if(!_held && !_uncapCheck && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*press\s+up\s*$")).Success)
        {
            yield return null;
            _upSel.OnInteract();
            if(_stagesRequired > 1 && _presses == _requiredPresses && _presses % 2 == 0 && _stagesDone + 1 >= _stagesRequired)
                yield return "awardpointsonsolve " + 8 * (_stagesRequired - 1);
            yield break;
        }

        if(!_held && !_uncapCheck && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*press\s+down\s*$")).Success)
        {
            yield return null;
            _downSel.OnInteract();
            if(_stagesRequired > 1 && _presses == _requiredPresses && _presses % 2 == 1 && _stagesDone + 1 >= _stagesRequired)
                yield return "awardpointsonsolve " + 8 * (_stagesRequired - 1);
            yield break;
        }

        if(!_uncapCheck && _stagesRequired == 1 && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*uncap\s*$")).Success)
        {
            yield return null;
            yield return "sendtochat Are you sure about that? Send the command again to confirm, or any other command to cancel. (You will get 3 stages.)";
            _uncapCheck = true;
            yield break;
        }

        if(!_uncapCheck && _stagesRequired == 3 && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*uncap\s*$")).Success)
        {
            yield return null;
            yield return "sendtochat Again? Are you sure? (You will get 5 stages.)";
            _uncapCheck = true;
            yield break;
        }

        if(!_uncapCheck && _stagesRequired == 5 && (m = Regex.Match(command.ToLowerInvariant(), @"^\s*uncap\s*$")).Success)
        {
            yield return "sendtochat No more.";
            yield break;
        }

        if(_uncapCheck)
        {
            yield return null;
            if((m = Regex.Match(command.ToLowerInvariant(), @"^\s*uncap\s*$")).Success)
            {
                yield return "sendtochat You've asked for it...";
                _stagesRequired += 2;
                _audio.PlaySoundAtTransform("Uncap", transform);
                _uncapCheck = false;
            }
            else
            {
                yield return "sendtochat Aborting uncap.";
                _uncapCheck = false;
            }
            yield break;
        }

        yield break;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if(_held)
        {
            while(!_releaseRule() || Time.time - _holdTime < 2f)
                yield return true;
            _buttonSel.OnInteractEnded();
        }
        while(!_isSolved)
        {
            if(_presses > _requiredPresses)
                yield break;
            while(_presses < _requiredPresses)
            {
                while(!_rule())
                    yield return true;
                _buttonSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
                while(!_releaseRule() || Time.time - _holdTime < 2f)
                    yield return true;
                _buttonSel.OnInteractEnded();
                yield return new WaitForSeconds(0.1f);
            }
            if(_presses % 2 == 0)
            {
                _upSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                _downSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
