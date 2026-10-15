using System;
using Verse;
using RimWorld;

namespace Psychology;

// Always-on trait thoughts (e.g. depressive Natural Mood) that Antidepressants should halve.
// BUG (pre-existing, shipped in 1.6): it tests stages[0].baseOpinionOffset < 0, but these are mood thoughts with an
// opinion offset of 0, so the halving never happens. (The unreleased Jan 2023 code tests baseMoodEffect instead.)
public class ThoughtWorker_AlwaysActivePsychology : ThoughtWorker_AlwaysActive
{
    public override float MoodMultiplier(Pawn p)
    {
        float num = base.MoodMultiplier(p);
        //Log.Message("ThoughtWorker_AlwaysActivePsychology, MoodMultiplier activated");
        if (p.health?.hediffSet?.HasHediff(HediffDefOfPsychology.Antidepressants) != true)
        {
            return num;
        }
        if (this.def.stages[0].baseOpinionOffset < 0f)
        {   
            num *= 0.5f;
        }
        return num;
    }
}

