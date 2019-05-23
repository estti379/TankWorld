﻿using System;
using System.Collections.Generic;

namespace TankWorld.src.ressources
{
    abstract public class Scene: IUpdate, IRender
    {

        //Constructors
        public Scene()
        {
        }

        //Accessors

        //Methods
        abstract public void Enter();
        abstract public void Exit();

        abstract public void HandleInput(InputStruct input);

        abstract public void Update();
        abstract public void Render();
    }
}
