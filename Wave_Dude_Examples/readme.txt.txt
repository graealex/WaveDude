Wave_Dude_2D Example projects V01

To install the project, copy the folders to the Project Root folder set in the Wave Dude 2D app (using the file menu or pick/browse root buttons. You might have to click to another tab or restart the app to make the installed projects visible.

THe Zip file also contains a folder named "Scene_example_images". This folder contains a set of scenes that can be used in any project. 

About Scene Images
Currently, scene images can only be created externally in a program like for example Photoshop. Save the images in a lossless format like .png. To add objects to a scene that the waves can interact with, you can use the red and green channels in the image color palette:

   - Red channel (Refractive index): can effectively use values ranging from 100 to 255, which represent refractive index values ranging from 1.00 to 2.55. Values lower than 100 will be set to 100 as a minimum. The red channel can be used to refract or reflect waves and create for example lenses.
   - Blue channel (Damping): reserved for damping - use values between 0 and 255, where zero means no damping and 255 heavy damping. It can be used to create regions that absorb waves like for example borders.
   - Green channel (Sources): reserved for creating wave sources, where the values 1-255 represent the frequency of the waves emitted by a pixel. If you use in-scene sources, do not use anti-aliasing to create them since this can cause the pixels next to a source to also contain a (lower) value in the green channel. When you want to use in-image sources, you must have 'use image sources' checked in the setup tab.

More info on the app can be found in the main menu under Info > Help or watch the explainer video on Youtube: https://youtu.be/yC2tx5x6vOY

