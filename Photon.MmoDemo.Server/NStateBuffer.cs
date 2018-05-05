using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Photon.MmoDemo.Common;
using Photon.MmoDemo.Server;
// a snapshot of values received over the network
public struct NetworkState
{
    public Vector pos;
    public float totalMs;
    public NetworkState(Vector pos, float time)
    {
        this.pos = pos;
        this.totalMs = time;
    }
}


public class NStateBuffer
{

    public int bufferSize;
    private NetworkState[] posBuffer;
    private int currBufferIndex = -1;
    public int posSetCount = 0;

    public NStateBuffer(int size)
    {
        bufferSize = size;
        posBuffer = new NetworkState[size];
    }

    public void AddNetworkState(Vector pos, float totalMs)
    {
        NetworkState ns = new NetworkState(pos, totalMs);
        currBufferIndex++;
        currBufferIndex = currBufferIndex % bufferSize;
        if (posSetCount < bufferSize)
            posSetCount++;
        posBuffer[currBufferIndex] = ns;
    }

    // get position, from buffer, that goes back to the timestamp totalms
    public Vector GetRewindedPos(float totalms)
    {
        if (currBufferIndex < 0)
        {
            GlobalVars.log.InfoFormat("warning no network states available");
            return posBuffer[currBufferIndex].pos;
        }

        if (posSetCount <= 3)
        {
            GlobalVars.log.InfoFormat("warning: rewind requested when buffer is near empty");
            return posBuffer[currBufferIndex].pos;
        }
        //        else if (posSetCount == 1)
        //        {
        //            Debug.Log("warning: rewind requested when buffer has few elements");
        //            return posBuffer[currBufferIndex].pos;
        //        }
        else//generic case
        {
            if (posSetCount == 2)
                GlobalVars.log.InfoFormat("pcount 2");
            // get the interval of the time between two buffer recordings to use as an estimate of all intervals
            float lastTime = posBuffer[currBufferIndex].totalMs;
            int indexOfTimeBeforeLast = currBufferIndex == 0 ? bufferSize - 1 : currBufferIndex - 1;
            double bufferIntervalEstimate = Math.Abs(lastTime - posBuffer[indexOfTimeBeforeLast].totalMs);
            float timeDiff = lastTime - totalms;
            int stepsBack = (int)Math.Floor(timeDiff / bufferIntervalEstimate); //estimated steps back in buffer
            if (stepsBack > bufferSize)
            {
                GlobalVars.log.InfoFormat("error: rewind requested more than pbuffsize steps back, some one is lagged out?");
                int indexOfOldest = (currBufferIndex + 1 - posSetCount);
                if (indexOfOldest < 0)
                    indexOfOldest = bufferSize + indexOfOldest;
                //   if (posBuffer[indexOfOldest].totalMs < totalms)
                //        Debug.Log("requested time is greater than the oldest known time so we could have found lerped it");


                if (totalms > posBuffer[currBufferIndex].totalMs)
                    return posBuffer[currBufferIndex].pos;

                int ansIndexRight = RewindHelperGetRightIndex(totalms, bufferSize - 3);
                int ansIndexLeft = (ansIndexRight - 1) % bufferSize;
                if (ansIndexLeft < 0)
                    ansIndexLeft = bufferSize + ansIndexLeft;
                if (totalms <= posBuffer[ansIndexRight].totalMs && totalms > posBuffer[ansIndexLeft].totalMs)
                {
                    float tlerp = (float)((totalms - posBuffer[ansIndexLeft].totalMs) /
                                          (posBuffer[ansIndexRight].totalMs - posBuffer[ansIndexLeft].totalMs));
                    //return posBuffer[ansIndexRight].pos * tlerp + posBuffer[ansIndexLeft].pos * (1 - tlerp);
                    return Vector.Lerp(posBuffer[ansIndexLeft].pos, posBuffer[ansIndexRight].pos, tlerp);
                }
                else
                {
                    GlobalVars.log.InfoFormat("ERROR rewind something went wrong");
                    return posBuffer[ansIndexRight].pos;
                }
            }
            else if (stepsBack > posSetCount) //stepping back more than we have buffer recordings
            {
                GlobalVars.log.InfoFormat("warning: potentially rewinding back further than we have info for");
                // start at the oldest position and if we need to move to newer positions until we have a match, do so
                int ansBufferIndex = RewindHelperGetRightIndex(totalms, stepsBack + 4);
                return posBuffer[ansBufferIndex].pos;
            }
            else
            {

                if (totalms > posBuffer[currBufferIndex].totalMs)
                    return posBuffer[currBufferIndex].pos;

                int ansIndexRight = RewindHelperGetRightIndex(totalms, stepsBack + 4);
                int ansIndexLeft = (ansIndexRight - 1) % bufferSize;
                if (ansIndexLeft < 0)
                    ansIndexLeft = bufferSize + ansIndexLeft;
                if (totalms <= posBuffer[ansIndexRight].totalMs && totalms > posBuffer[ansIndexLeft].totalMs)
                {
                    float tlerp = (float)((totalms - posBuffer[ansIndexLeft].totalMs) /
                        (posBuffer[ansIndexRight].totalMs - posBuffer[ansIndexLeft].totalMs));
                    //return posBuffer[ansIndexRight].pos * tlerp + posBuffer[ansIndexLeft].pos * (1 - tlerp);
                    return Vector.Lerp(posBuffer[ansIndexLeft].pos, posBuffer[ansIndexRight].pos, tlerp);
                }
                else
                {
                    GlobalVars.log.InfoFormat("ERROR rewind something went wrong");
                    return posBuffer[ansIndexRight].pos;
                }

            }
        }
    }

    // to be optimized this should only be called if totalMS is smaller thant the largest timestamp in buffer
    private int RewindHelperGetRightIndex(float totalMS, int stepsBack)
    {
        int ansBufferIndex = (currBufferIndex - Math.Min(posSetCount - 1, stepsBack)) % bufferSize;
        if (ansBufferIndex < 0)
            ansBufferIndex = bufferSize + ansBufferIndex;
        int ansIndexLeft;
        float rightIndexTime;
        int ansIndexRight;
        do
        {
            ansBufferIndex++;
            ansIndexRight = ansBufferIndex % bufferSize;
            rightIndexTime = posBuffer[ansIndexRight].totalMs;
            ansIndexLeft = (ansBufferIndex - 1) % bufferSize;
            if (ansIndexLeft < 0)
                ansIndexLeft = bufferSize + ansIndexLeft;
            if (ansBufferIndex > bufferSize * 2)    //todo: remove if it doesnt happen
            {
                GlobalVars.log.InfoFormat("error: rewind infinite loop situation");
                return (currBufferIndex + 2) % bufferSize;
            }

            if (rightIndexTime >= totalMS && posBuffer[ansIndexLeft].totalMs < totalMS)
                return ansIndexRight;
        } while (true);
    }

}
