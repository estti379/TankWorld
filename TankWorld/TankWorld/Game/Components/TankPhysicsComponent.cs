﻿

using System;
using System.Collections.Generic;
using TankWorld.Engine;
using TankWorld.Game.Items;
using static SDL2.SDL;

namespace TankWorld.Game.Components
{
    class TankPhysicsComponent: PhysicsComponent
    {
        //private HitBoxStruct hitBoxes;
        private int heightBox;
        private int widthBox;

        //Constructors
        public TankPhysicsComponent(TankObject owner)
        {
            InitializeHitBoxes(owner);
        }


        //Accessors

        //Methods
        override public void Update(GameObject parentObject, ref WorldItems world)
        {
            CheckForCloseness(parentObject, ref world);
            TankObject parentTank = parentObject as TankObject;
            UpdateHitBox(parentTank);
        }

        public void InitializeHitBoxes(TankObject parent)
        {
            HitBox bulletHitBox = new HitBox
            {
                boxType = HitBox.Type.RECTANGLE,
            };

            HitBoxes.hitBoxesList.Add("Tank", bulletHitBox);
            UpdateHitBox(parent);
        }

        public void UpdateHitBox(TankObject parent)
        {
            HitBoxes.Position = parent.Position;
            heightBox = parent.Model.AllSprites["TankBody"].Pos.h;
            widthBox = parent.Model.AllSprites["TankBody"].Pos.w;
            if (heightBox >= widthBox)
            {
                HitBoxes.CollisionRange = heightBox;
            }
            else
            {
                HitBoxes.CollisionRange = widthBox;
            }

            double angle = parent.DirectionBody;

            HitBoxes.hitBoxesList["Tank"] = Helper.UpdateRectangleHitBox(HitBoxes.hitBoxesList["Tank"], HitBoxes.Position, angle, widthBox, heightBox);
            //HitBoxes.hitBoxesList["Tank"] = Helper.UpdateCircleHitBox(HitBoxes.hitBoxesList["Tank"], HitBoxes.Position, widthBox/2);
        }

        
    }
}
