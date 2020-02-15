# pirates

This is a simple pirate game I made in my spare time. You fight against an AI pirate ship and try not to get sunk!

I made this in Unity, so the code is mostly in C#. I also wrote a few shaders in CG, which provide graphics for different objects like the ocean. (I provide a bunch of explanations of how it works below!)

The pirate ship model was made by me using [Blender](https://blender.org) and is based roughly off of [the caravel](https://nautarch.tamu.edu/shiplab/01George/index.htm), which was a small ship in the 15th and 16th century. It was known for being light and fast, which I suppose would be appreciated by a pirate if they ever started getting chased by a bigger ship.

I never completely finished this project and stopped working on it, so it's still a bit rough around the edges and there isn't really much content at all anyways. I might return to it later and make it into a proper game, but right now I'm working on other stuff.

![screenshot](https://i.imgur.com/3nDo1ji.png)

![screenshot GIF](https://i.imgur.com/EbdAkm0h.gif)

In the rest of this readme, I'll discuss how I implemented some of the algorithms in the pirate game to make it look (semi) realistic and fun to play.

- [how I made the ocean](#how-i-made-the-ocean)
- [how I made objects float in a realistic manner](#how-i-implemented-buoyancy)

## Installation

You can run this game locally by cloning the repo then opening it in Unity version 2019.2.6f1.

## How I made the ocean

One of the primary elements if you are playing pirate game is the fact that you are constantly surrounded by water. Therefore, I spent a bunch of time trying to make sure the water looked and felt right.

(Note: in this section, I'm primarily talking about the water itself and not how objects interact with the water)

When you're looking to make good-looking water, there are pretty much two basic components you have to nail down before you get anywhere else: the actual shape (geometry) of the waves, and then also to some extent the shading and color of the waves. On top of that you can have interactions with objects like foam around things floating in the water, but for now I've mostly just focused on the first two things for the water.

To start out, I researched a bunch of different ways how you can simulate ocean waves in a video game. (Basically, this just amounted to using google and reddit to look at ways other video games simulated water.) One of the most interesting things I found is a technique of simulating ocean waves called "Tessendorf waves," which was described in a paper by Jerry Tessendorf and uses a mathematical concept called Fast Fourier Transform to precompute (I think) a heightmap for the ocean. However, you have to know some more advanced mathematics, and at that time all I knew was Calculus I and didn't to spend weeks learning math just to implement some waves when I could be working on something else in my game.

I looked for other methods and another technique I found was called Gerstner Waves. This is also a way to simulate ocean waves, but doesn't require you to actually be smart or know stuff, since it all it involves is summing a bunch of sine functions. Then you can simply use that to offset each vertex in the ocean mesh. I mostly referenced the [NVIDIA GPU Gems](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) online book, which explains the basic formulas for this approach, and it also helps you out by giving extra equations which can be used to compute the water's surface normals and things like that.

From there, the key is to use the position function they give you `P(x,y,t)` to offset each vertex of the ocean mesh every frame. Keep in mind this doesn't just offset the vertices vertically, but also moves them horizontally, which helps to avoid the sea from looking like a giant sine wave. This can be done using C#, which I did initially, but eventually wrote a vertex shader that offsets the vertices on the GPU. This has the advantage that it is much faster than performing calculations on the CPU, since it can very easily process multiple vertices in parallel.

The only major challenge I had with Gerstner Waves is that since they aren't completely based on a physical simulation, you have to explicitly state the parameters for the waves in order for them to look good. I played around with it for a few hours and think I've gotten something that looks somewhat realistic, though obviously I'd have to spend a bunch more time if I wanted it to look perfect or good under different weather conditions.

If you want to see the code for the ocean simulation, check out the [ocean.shader](https://github.com/maxematical/pirates/blob/master/Assets/Shaders/Ocean.shader) file I have in the repository. For the user-configurable parameters of the ocean, I pass them in as uniforms from my C# script. Please note all of this is licensed under all rights reserved, so don't copy any of the code, but if you're trying to implement Gerstner Waves maybe it will be helpful as a guide just to see if you've got any of your code wrong.

Anyways, the other half(!) of making the ocean had to do with lighting and coloring it properly. This, again, I am not 100% satisfied with and might return to later, but for now it looks decent enough that I am happy with it.

If you have read much about 3D rendering, the basic principles of shading the ocean are generally pretty simple, so it was mostly just a matter of combining these principles in the right way to get a nice looking ocean.

For starters, I had your basic Lambertian lighting model with a nice blue diffuse color and a bit of specular component for the sun. Then I added some subsurface scattering, which if you haven't heard of this before, is basically when light passes through a mostly solid object and comes out tinted -- think of holding your hand over a flashlight and seeing that your fingers look bright red and a bit glowing. I also added some fresnel effect (pronounced fruh-nel), which is when a the part of an object you are looking at from the side appears slightly lighter than the part you are looking at head-on.

This looked okay, but it still didn't exactly look like an ocean. When you think about it, real-life seawater is full of foam, grease, and random bits of stuff floating in the water. The above principles get you something that mimics water, but doesn't have all the details you would expect in water.

Again, I haven't gotten it perfect yet, but the main thing I've done to combat this is to add a bit of foam to the water, especially in areas just after a wave where you would expect it to be more frothy there. I did this by taking a texture with some [cell noise](https://en.wikipedia.org/wiki/Worley_noise) and then sampling it at two different UV coordinates. I then combine the two samples to get a foam pattern that looks somewhat random and in motion. I add some of this value into the water's base surface color, depending on how slanted the surface of the water is at that given point.

If you want to see the code for the surface of the water, check out the same [ocean.shader](https://github.com/maxematical/pirates/blob/master/Assets/Shaders/Ocean.shader) file. The code pertaining to the surface of the water is mostly within the `surf` and `LightingSubsurf` functions. (Thanks to Unity's great abundance of features, especially compared to other game engines like Unreal, I had to implement my own lighting function to get subsurface scattering working)

## How I implemented buoyancy

This README is a work in progress, but trust me this section will be really awesome if I ever decide to actually finish it

![physics simulation demo](https://i.imgur.com/p1xw45E.gif)

## Features

- an AI boat that circles the player and shoots cannonballs at them
- cartoonish but decent looking water simulation
- buoyancy physics algorithm that ensures objects and ships in the water move in the water in a realistic manner

## Contributing

Unfortunately, I am a pirate and don't take contributions

## License

(c) Max Battle 2019, all rights reserved.