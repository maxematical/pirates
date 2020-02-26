# pirates

This is a simple pirate game I made in my spare time. You fight against an AI pirate ship and try not to get sunk!

I made this in Unity, so the code is mostly in C#. I also wrote a few shaders in CG, which provide graphics for different objects like the ocean. (I provide a bunch of explanations of how it works below!)

The pirate ship model was made by me using [Blender](https://blender.org) and is based roughly off of [the caravel](https://nautarch.tamu.edu/shiplab/01George/index.htm), which was a small ship in the 15th and 16th century. It was known for being light and fast, which I suppose would be appreciated by a pirate if they ever started getting chased by a bigger ship.

I never completely finished this project and stopped working on it, so it's still a bit rough around the edges and there isn't really much content at all anyways. I might return to it later and make it into a proper game, but right now I'm working on other stuff.

Click on this image for a video:

[<img src="https://i.imgur.com/3nDo1ji.png" alt="screenshot of boats fighting" width="600" />](https://imgur.com/EbdAkm0)

In the rest of this readme, I'll discuss how I implemented some of the algorithms in the pirate game to make it look (semi) realistic and fun to play.

- [how I made the ocean](#how-i-made-the-ocean)
- [how I made objects float in a realistic manner](#how-i-implemented-buoyancy)

## Installation

You can run this game locally by cloning the repo then opening it in Unity version 2019.2.6f1.

## How I made the ocean

One of the primary elements if you are playing pirate game is the fact that you are constantly surrounded by water. Therefore, I spent a bunch of time trying to make sure the water looked and felt right.

(Note: in this section, I'm primarily talking about the water itself and not how objects interact with the water. Also, the below paragraphs are meant more as a "high-level" explanation of how everything works, because I'm mostly mentioning the different concepts and algorithms I used for the water. I don't give many code examples since for pretty much every one of these concepts, you can just type it into google and find a tutorial on how to implement it. In fact, that's actually exactly what I did when I was writing the code!)

When you're looking to make good-looking water, there are pretty much two basic components you have to nail down before you get anywhere else: the actual shape (geometry) of the waves, and then also to some extent the shading and color of the waves. On top of that you can have interactions with objects like foam around things floating in the water, but for now I've mostly just focused on the first two things for the water.

To start out, I researched a bunch of different ways how you can simulate ocean waves in a video game. (Basically, this just amounted to using google and reddit to look at ways other video games simulated water.) One of the most interesting things I found is a technique of simulating ocean waves called "Tessendorf waves," which was described in a paper by Jerry Tessendorf and uses a mathematical concept called Fast Fourier Transform to precompute (I think) a heightmap for the ocean. However, you have to know some more advanced mathematics, and at that time all I knew was Calculus I and didn't to spend weeks learning math just to implement some waves when I could be working on something else in my game.

I looked for other methods and another technique I found was called Gerstner Waves. This is also a way to simulate ocean waves, but doesn't require you to actually be smart or know stuff, since it all it involves is summing a bunch of sine functions. Then you can simply use that to offset each vertex in the ocean mesh. I mostly referenced the [NVIDIA GPU Gems](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) online book, which explains the basic formulas for this approach, and it also helps you out by giving extra equations which can be used to compute the water's surface normals and things like that.

From there, the key is to use the position function they give you `P(x,y,t)` to offset each vertex of the ocean mesh every frame. Keep in mind this doesn't just offset the vertices vertically, but also moves them horizontally, which helps to avoid the sea from looking like a giant sine wave. This can be done using C#, which I did initially, but eventually wrote a vertex shader that offsets the vertices on the GPU. This has the advantage that it is much faster than performing calculations on the CPU, since it can very easily process multiple vertices in parallel.

```hlsl
// The basic code of how to move a vertex by the position function, to implement Gerstner Waves

for (int i = 0; i < numberWaves; i++)
{
	float steepness, amplitude, frequency, phaseConstant = /* settings of this wave */;
	float3 direction = /* direction of this wave */;

	pos.x += steepness * amplitude * direction.x * cos(dot(frequency * direction, xz) + phaseConstant * time);
	pos.z += steepness * amplitude * direction.z * cos(dot(frequency * direction, xz) + phaseConstant * time);
	pos.y += amplitude * sin(dot(frequency * direction, xz) + phaseConstant * time);
}
```

The only major challenge I had with Gerstner Waves is that since they aren't completely based on a physical simulation, you have to explicitly state the parameters for the waves in order for them to look good. I played around with it for a few hours and think I've gotten something that looks somewhat realistic, though obviously I'd have to spend a bunch more time if I wanted it to look perfect or good under different weather conditions.

The code for the ocean simulation is stored in the [ocean.shader](https://github.com/maxematical/pirates/blob/master/Assets/Shaders/Ocean.shader) file. The user-configurable parameters of the ocean are passed in as uniforms from my C# script.

(Please note all of this is TECHNICALLY licensed under all rights reserved so try to use this as more a model for your code rather than copying and pasting!)

<img src="https://i.imgur.com/ZslvYEc.png" alt="ocean picture" width="600" />

Anyways, the other half(!) of making the ocean had to do with lighting and coloring it properly. This, again, I am not 100% satisfied with and might return to later, but for now it looks decent enough that I am happy with it.

If you have read much about 3D rendering, the basic principles of shading the ocean are generally pretty simple, so it was mostly just a matter of combining these principles in the right way to get a nice looking ocean.

The water shader uses the Lambert lighting model with a nice blue diffuse color and a bit of specular component for the sun's reflections. It also uses subsurface scattering, which is basically the effect of light passing into an object, spreading out inside of it, and then exiting and causing a "glowy" effect. (An example of this is when you hold your hand over a flashlight and your fingers become glowing red.) The water also has a small amount of Fresnel effect, which is when a the part of an object you are looking at from the side appears slightly lighter than the part you are looking at head-on.

These principles were generally relatively straightforward, and it was possible to google how to implement these fairly easy. Again, most of the difficulty in this was just tweaking the parameters so the water looked right. This is 90% of the water shading code and it works decently well.

<img src="https://i.imgur.com/M0w8xbd.png" alt="ocean picture" width="600" />

However, with ONLY these principles, the water still didn't exactly look like an ocean. When you think about it, real-life seawater is full of foam, grease, and random bits of stuff floating in the water. The above princples still don't have all the details you would expect in a real ocean.

Again, I haven't gotten it perfect yet, but the main thing I've done so far to combat this is to add a bit of foam to the water. I did this by taking a texture with some [cell noise](https://en.wikipedia.org/wiki/Worley_noise) and then sampling it at two different UV coordinates. I then combine the two samples to get a foam pattern that looks somewhat random and in motion.

To enhance the illusion of frothy, foamy water, I vary the amount of foam depending on how slanted the surface of the water is at a given point, which also happens to be right before or after the crest of a wave, which is where you would expect more foam in real life.

That being said, if I were to work on this agian, I'd still probably try to improve the foam a bunch more. Although it does make the water a lot better, I still don't think it's quite enough to combat the "plastic-y" look of the water shader right now.

If you want to see the code for the surface of the water, check out the same [ocean.shader](https://github.com/maxematical/pirates/blob/master/Assets/Shaders/Ocean.shader) file. The code pertaining to the surface of the water is mostly within the `surf` and `LightingSubsurf` functions. <sup>(Thanks to Unity's great abundance of features, especially compared to other game engines like Unreal, I had to implement my own lighting function to get subsurface scattering working)</sup>

## How I implemented buoyancy

This README is a work in progress, but trust me this section will be really awesome if I ever decide to actually finish it

[Physics simulation demo](https://imgur.com/p1xw45E)

## Features

- an AI boat that circles the player and shoots cannonballs at them
- cartoonish but decent looking water simulation
- buoyancy physics algorithm that ensures objects and ships in the water move in the water in a realistic manner

## License

(c) Max Battle 2019, all rights reserved.