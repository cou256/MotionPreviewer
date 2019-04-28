/**
 * ModelChacker
 *
 * @author     @Crimson_Apple
 * @copyright  2010-2019 @Crimson_Apple
 * @license    http://www.opensource.org/licenses/mit-license.html  MIT License
 * @version    1.0
**/

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.UI;

using UniRx;

#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// ModelChacker 
/// </summary>
public class ModelChacker : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] List<string> resources;

    [SerializeField] GameObject uiPrerab;
    [SerializeField] GameObject layoutPrerab;
    [SerializeField] GameObject gridPrerab;
    [SerializeField] GameObject buttonPrerab;
    [SerializeField] AnimationCurve blendingCurve;

    PlayableGraph playableGraph;
    List<Mixer> mixers = new List<Mixer>();

    void Awake()
    {
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        playableGraph = PlayableGraph.Create(GetType().ToString());
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        var animationLayerMixerPlayable = AnimationLayerMixerPlayable.Create(playableGraph, resources.Count);
        var animationPlayableOutput = AnimationPlayableOutput.Create(playableGraph, GetType().ToString() + "Output", animator);
        animationPlayableOutput.SetSourcePlayable(animationLayerMixerPlayable);

        var ui = Instantiate(uiPrerab);
        var layout = Instantiate(layoutPrerab, ui.transform);

        resources.ForEach(resource =>
        {
            var animationClips = Resources.LoadAll<AnimationClip>(resource).ToList();
            var mixier = new Mixer(playableGraph, ref animationLayerMixerPlayable, blendingCurve, animationClips);
            var grid = Instantiate(gridPrerab, layout.transform);
            var toggleGroup = grid.GetComponent<ToggleGroup>();

            animationClips.ForEach(animationClip =>
            {
                var button = Instantiate(buttonPrerab, grid.transform);
                button.GetComponentInChildren<Text>().text = animationClip.name;
                var toggle = button.GetComponent<Toggle>();
                toggle.group = toggleGroup;
                toggle
                    .OnValueChangedAsObservable()
                    .Where(_ => _)
                    .Subscribe(_ => mixier.Play(animationClip.name));
            });
            mixers.Add(mixier);
        });
    }
    void OnDestroy()
    {
        playableGraph.Destroy();
    }
    void Start()
    {
        playableGraph.Play();
    }
    class Mixer
    {

        Dictionary<string, int> index = new Dictionary<string, int>();
        Dictionary<string, float> times = new Dictionary<string, float>();
        List<AnimationClipPlayable> animationClipPlayables = new List<AnimationClipPlayable>();
        AnimationCurve blendingCurve;

        AnimationMixerPlayable animationMixerPlayable;
        string prev, next;
        float currentTime;

        public Mixer(PlayableGraph playableGraph, ref AnimationLayerMixerPlayable animationLayerMixerPlayable, AnimationCurve blendingCurve, List<AnimationClip> animationClips)
        {
            animationMixerPlayable = AnimationMixerPlayable.Create(playableGraph, animationClips.Count, true);
            animationLayerMixerPlayable.AddInput(animationMixerPlayable, 0, 1);
            this.blendingCurve = blendingCurve;

            for (var i = 0; i < animationClips.Count; i++)
            {
                var animationClipPlayable = AnimationClipPlayable.Create(playableGraph, animationClips[i]);
                animationMixerPlayable.ConnectInput(i, animationClipPlayable, 0);

                animationClipPlayables.Add(animationClipPlayable);
                index.Add(animationClips[i].name, i);
                times.Add(animationClips[i].name, animationClips[i].length);
            }

            Observable
                .EveryUpdate()
                .Subscribe(_ =>
                {
                    for (var i = 0; i < animationClipPlayables.Count; i++)
                    {
                        animationMixerPlayable.SetInputWeight(i, 0);
                    }
                    if (string.IsNullOrEmpty(prev))
                    {
                        animationMixerPlayable.SetInputWeight(index[next], 1.0f);
                    }
                    else
                    {
                        var dt = Time.timeSinceLevelLoad - currentTime;
                        var ratio = blendingCurve.Evaluate(dt);
                        animationMixerPlayable.SetInputWeight(index[prev], 1 - ratio);
                        animationMixerPlayable.SetInputWeight(index[next], ratio);
                    }
                });
        }
        public void Play(string name)
        {
            currentTime = Time.timeSinceLevelLoad;
            prev = next;
            next = name;
        }
    }
}