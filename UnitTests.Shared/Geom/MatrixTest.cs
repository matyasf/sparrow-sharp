﻿using System;
using NUnit.Framework;
using Sparrow.Geom;
using Sparrow.Utils;

namespace Tests
{
    // note: Assert.Equals is a method of object, its not for unit tests
    [TestFixture]
    public class MatrixTest : TestBase
    {

        Matrix2D countMatrix;
        Matrix2D identMatrix;

        [SetUp]
        protected void SetUp()
        {
            countMatrix = Matrix2D.Create(1, 2, 3, 4, 5, 6);
            identMatrix = Matrix2D.Create();
        }

        [Test]
        public void TestInit()
        {
            bool isEqual = CheckMatrixValues(countMatrix, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);
            Assert.IsTrue(isEqual, "wrong matrix " + countMatrix);

            isEqual = CheckMatrixValues(identMatrix, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f);
            Assert.IsTrue(isEqual, "wrong matrix " + identMatrix);
        }

        [Test]
        public void TestCopy()
        {
            Matrix2D copy = Matrix2D.Create();
            copy.CopyFromMatrix(countMatrix);

            Assert.IsTrue(countMatrix.IsEqual(copy), "copy not equal " + copy);
            Assert.IsFalse(countMatrix == copy, "its not a copy, but the same object");
        }

        [Test]
        public void TestAppendMatrix()
        {
            Matrix2D copy = Matrix2D.Create();

            copy.CopyFromMatrix(countMatrix);
            copy.AppendMatrix(identMatrix);
            Assert.IsTrue(countMatrix.IsEqual(copy), "multiplication with identity modified matrix");

            copy.CopyFromMatrix(identMatrix);
            copy.AppendMatrix(countMatrix);
            Assert.IsTrue(countMatrix.IsEqual(copy), "multiplication with identity modified matrix");

            Matrix2D countdownMatrix = Matrix2D.Create(9, 8, 7, 6, 5, 4);
            copy.AppendMatrix(countdownMatrix);
            Assert.IsTrue(CheckMatrixValues(copy, 23, 20, 55, 48, 92, 80), "wrong matrix " + copy);

            countdownMatrix.AppendMatrix(countMatrix);
            Assert.IsTrue(CheckMatrixValues(countdownMatrix, 33, 50, 25, 38, 22, 32), "wrong matrix " + copy);
        }

        [Test]
        public void TestInvert()
        {
            countMatrix.Invert();
            Assert.IsTrue(CheckMatrixValues(countMatrix, -2, 1, 3.0f/2.0f, -0.5f, 1, -2), "invert produced wrong result: " + countMatrix);

            Matrix2D translateMatrix = Matrix2D.Create();
            translateMatrix.Translate(20.0f, 40.0f);
            translateMatrix.Invert();

            Assert.IsTrue(CheckMatrixValues(translateMatrix, 1, 0, 0, 1, -20, -40), "invert produced wrong result: " + translateMatrix);
        }

        [Test]
        public void TestTranslate()
        {
            identMatrix.Translate(5, 7);
            Point point = Point.Create(10, 20);
            Point tPoint = identMatrix.TransformPoint(point);

            Assert.IsTrue(MathUtil.Equals(15, tPoint.X), "wrong X value " + tPoint.X);
            Assert.IsTrue(MathUtil.Equals(27, tPoint.Y), "wrong Y value " + tPoint.Y);
        }

        [Test]
        public void TestRotate()
        {
            identMatrix.Rotate((float)Math.PI / 2.0f);
            Point point = Point.Create(10, 0);
            Point rPoint = identMatrix.TransformPoint(point);
            Assert.IsTrue(MathUtil.Equals(0, rPoint.X), "wrong X value " + rPoint.X);
            Assert.IsTrue(MathUtil.Equals(10, rPoint.Y), "wrong Y value " + rPoint.Y);

            identMatrix.Identity();
            identMatrix.Rotate((float)Math.PI);
            point.Y = 20;
            rPoint = identMatrix.TransformPoint(point);
            Assert.IsTrue(MathUtil.Equals(-10, rPoint.X), "wrong X value " + rPoint.X);
            Assert.IsTrue(MathUtil.Equals(-20, rPoint.Y), "wrong Y value " + rPoint.Y);
        }

        [Test]
        public void TestScale()
        {
            identMatrix.Scale(2.0f, 0.5f);
            Point point = Point.Create(10, 20);
            Point sPoint = identMatrix.TransformPoint(point);

            Assert.IsTrue(MathUtil.Equals(20.0f, sPoint.X), "wrong X value " + sPoint.X);
            Assert.IsTrue(MathUtil.Equals(10.0f, sPoint.Y), "wrong Y value " + sPoint.Y);
        }

        [Test]
        public void TestConcatenatedTransformations()
        {
            identMatrix.Rotate((float)Math.PI / 2.0f);
            identMatrix.Scale(0.5f, 0.5f);
            identMatrix.Translate(0.0f, 5.0f);

            Point ctPoint = identMatrix.TransformPoint(10, 0);

            Assert.IsTrue(MathUtil.Equals(0.0f, ctPoint.X), "wrong X value " + ctPoint.X);
            Assert.IsTrue(MathUtil.Equals(10.0f, ctPoint.Y), "wrong Y value " + ctPoint.Y); 
        }

        bool CheckMatrixValues(Matrix2D matrix, float a, float b, float c, float d, float tx, float ty)
        {
            return MathUtil.Equals(a, matrix.A) && MathUtil.Equals(b, matrix.B) &&
                MathUtil.Equals(b, matrix.B) && MathUtil.Equals(c, matrix.C) &&
                MathUtil.Equals(tx, matrix.Tx) && MathUtil.Equals(ty, matrix.Ty);
        }

    }
}

