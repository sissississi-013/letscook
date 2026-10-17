# Vision

## In the user's words
> "I want the cooking game to be ultimate. Like maximum freedom. I can cook whatever I want to and there's limitless tools I could use. If there's something missing I can just prompt and it'd be ready to use very SOON. After you've cooked one dish it would unlock a new good recipe and a good course (interpreted by LLM what name is it) and good interesting name generated for it. I want it to be hella personal. Like Cooking Mama but a paidia. I want the human motion data for cooking and for handling all kinds of cook to be collected for robotics training."

> "I hope it's as realistic as possible too!"

## Pillars
1. **Paidia (free play):** no fixed recipe steps or fail states. Cook anything, any way.
2. **Unlimited tools:** if a tool is missing, ask for it in words and it appears in the kitchen, ready to use, soon after.
3. **AI-named progression:** after each finished dish, an LLM interprets what you made, decides its course, and gives it a memorable name. That unlocks a new recipe and a new course.
4. **Hella personal:** your cookbook becomes a record of *your* cooking. *(Guess, unconfirmed: unlocks build on your past dishes and style.)*
5. **As realistic as possible:** real-feeling physics for food and tools.
6. **Robot data as a side effect:** every chop, stir, pour, and pinch is logged as training data for robot hands.

## Inspirations
- Cooking Mama: charm, gesture-based cooking (but fixed steps, which this game drops)
- Surgeon Simulator / Cooking Simulator: physics comedy
- Paidia (Roger Caillois): unstructured, free play, as opposed to ludus (rule-bound games)

## Known tensions (to resolve)
- **Realism vs. unlimited tools:** each generated tool needs realistic mass, grip points, and an effect on food, not just a 3D shape.
- **Realistic food physics** (slicing, liquids, frying, dough) is the hardest kind of simulation. Decide what's simulated for real and what's faked.
- **Realism helps the robot data:** physics closer to a real kitchen gives more transferable demonstrations.
- **Timeline unknown:** is this a hackathon weekend build or a longer project? That decides scope.

## Guesses (not confirmed by the user)
- Funny, messy outcomes are welcome.
- The data collection should never interrupt play with "training task" moments.
