using System.Collections.Generic;

namespace EdcHost;

public class Car //选手的车
{
    // the object of package and picked by car and first collision time
    private class PackagesAndTime
    {
        public Package mPkg;
        public int mFirstCollisionTime;

        public PackagesAndTime(Package _pkg, int _FirstCollisionTime = -1)
        {
            mPkg = _pkg;
            mFirstCollisionTime = _FirstCollisionTime;
        }
    }

    public const int RUN_CREDIT = 10;          //小车启动可以得到10分;
    public const int PICK_CREDIT = 5;          //接到一笔订单得5分;
    public const int CHARGE_CREDIT = 5;        // credit for set a charge station
    public const int ON_BLACk_LINE_PENALTY = 10;
    public const int IN_OPPONENT_STATION_PENALTY = 10; // in cm per frame
    public const int IN_OBSTACLE_PENALTY = 10; // in cm per frame
    public const int MARK_PENALTY = 50;
    public const int MAX_PKG_COUNT = 5;
    public const int ENERGY_EXHAUSTION_PENALTY = 50; // 50 ms per cm



    public const int COLLISION_RADIUS = 8;
    public const int COLLISION_DETECTION_TIME = 1000; // in ms
    public const int MAX_MILEAGE = 2000; // in cm


    private MyQueue<Dot> mQueuePos;   // series of location
    public Camp mCamp;               //A or B get、set直接两个封装好的函数
    private int mScore;               //得分
    private int mMileage;              //小车续航里程
    private List<PackagesAndTime> mPickedPackages; // package picked by car

    //Flag of whether the car is able to run
    private bool mIsAbleToRun;


    // Flags of Location
    // Locations where car would get penalty
    private bool mIsOnBlackLine;
    private bool mIsInOpponentChargeStation;
    private bool mIsInObstacle;


    public MyQueue<bool> mFlagIsInChargeStation;


    private int mGameTime;


    /********************************************
    Interface
    *********************************************/
    public Car(Camp c)
    {
        mQueuePos = new MyQueue<Dot>(10);
        mCamp = c;
        mScore = 0;
        mMileage = MAX_MILEAGE;
        mPickedPackages = new List<PackagesAndTime>();

        // Flags
        mIsAbleToRun = false;
        mIsOnBlackLine = false;
        mIsInOpponentChargeStation = false;
        mIsInObstacle = false;
        mFlagIsInChargeStation = new MyQueue<bool>(10);

        mGameTime = -1;
    }

    public void Reset()
    {
        mQueuePos.Clear();

        mScore = 0;
        mMileage = MAX_MILEAGE;
        mPickedPackages.Clear();

        // Flags
        mIsAbleToRun = false;
        mIsOnBlackLine = false;
        mIsInOpponentChargeStation = false;
        mIsInObstacle = false;
        mFlagIsInChargeStation.Clear();

        mGameTime = -1;
    }

    public void Update(Dot _CarPos, int _GameTime, bool _IsOnBlackLine,
        bool _IsInObstacle, bool _IsInOpponentStation, bool _IsInChargeStation,
        ref List<Package> _PackagesRemain, out int _TimePenalty)
    {
        mGameTime = _GameTime;

        UpdatePos(_CarPos);
        int temp_TimePenalty = 0;
        //至少获取了两个位置之后(0.1s)才有后面的操作
        if (_GameTime > 1)
        {
            if (!mIsAbleToRun)
            {
                AbleToRun();
            }

            _TimePenalty = 0;

            //action
            PickPackage(_CarPos, ref _PackagesRemain);
            DropPackage(_CarPos);
            //这里不能直接写_TimePenalty，因为它必须写在最外层，故用临时变量temp_TimePenalty作为out
            UpdateMileage(out temp_TimePenalty);
            Charge(_IsInChargeStation);

            // Penalty
            OnBlackLinePenaly(_IsOnBlackLine);
            InOpponentStationPenalty(_IsInOpponentStation);
            InObstaclePenalty(_IsInObstacle);
        }
        _TimePenalty = temp_TimePenalty;

    }

    public int GetScore()
    {
        return mScore;
    }

    public void GetMark()
    {
        mScore -= MARK_PENALTY;
    }

    public void SetChargeStation()
    {
        mScore += CHARGE_CREDIT;
    }

    public Dot CurrentPos()
    {
        return mQueuePos.Item(-1);
    }

    public Package GetPackageOnCar(int _index)
    {
        if (_index >= mPickedPackages.Count)
        {
            return new Package();
        }
        else
        {
            return mPickedPackages[_index].mPkg;
        }
    }

    public int GetPackageCount()
    {
        return mPickedPackages.Count;
    }

    public int GetMileage()
    {
        return mMileage;
    }

    /********************************************
    Private Functions
    *********************************************/

    private void UpdatePos(Dot _CarPos)
    {
        mQueuePos.Enqueue(_CarPos);
    }

    private void AbleToRun()
    {
        if (!mIsAbleToRun && mQueuePos.Count() > 1 &&
        Dot.Distance(mQueuePos.Item(-1), mQueuePos.Item(-2)) > 0)
        {
            mScore += RUN_CREDIT;
            mIsAbleToRun = true;
        }
    }

    private void PickPackage(Dot _CarPos, ref List<Package> _PackagesRemain)      //拾取外卖
    {
        for (int i = 0; i < _PackagesRemain.Count; i++)
        {
            var pkg = _PackagesRemain[i];
            if (pkg.Distance2Departure(_CarPos) <= COLLISION_RADIUS &&
                mPickedPackages.Count <= MAX_PKG_COUNT)
            {
                mPickedPackages.Add(new PackagesAndTime(pkg));
                _PackagesRemain.Remove(pkg);
                mScore += PICK_CREDIT;
                break;
            }
        }
    }

    private void DropPackage(Dot _CarPos)      //送达外卖 
    {
        foreach (var PkgAndTime in mPickedPackages)
        {
            if (PkgAndTime.mPkg.Distance2Destination(_CarPos) <= COLLISION_RADIUS)
            {
                if (PkgAndTime.mFirstCollisionTime != -1 &&
                    mGameTime - PkgAndTime.mFirstCollisionTime > COLLISION_DETECTION_TIME)
                {
                    mPickedPackages.Remove(PkgAndTime);
                    mScore += PkgAndTime.mPkg.GetPackageScore(mGameTime);
                }
                else if (PkgAndTime.mFirstCollisionTime == -1)
                {
                    PkgAndTime.mFirstCollisionTime = mGameTime;
                }
            }
            else if (PkgAndTime.mFirstCollisionTime != -1)
            {
                PkgAndTime.mFirstCollisionTime = -1;
            }
        }
    }

    private void UpdateMileage(out int _Time_Penalty)
    {
        int DeltaDistance = Dot.Distance(mQueuePos.Item(-1), mQueuePos.Item(-2));
        mMileage -= DeltaDistance;
        if (mMileage < 0)
        {
            _Time_Penalty = DeltaDistance * ENERGY_EXHAUSTION_PENALTY;
        }
        else
        {
            _Time_Penalty = 0;
        }
    }

    private void Charge(bool IsInChargeStation)
    {
        mFlagIsInChargeStation.Enqueue(IsInChargeStation);
        for (int i = 0; i < mFlagIsInChargeStation.Count(); i++)
        {
            if (!mFlagIsInChargeStation.Item(i))
            {
                return;
            }
        }

        mMileage = MAX_MILEAGE;
    }

    private void OnBlackLinePenaly(bool IsOnBlackLine)
    {
        if (IsOnBlackLine && !mIsOnBlackLine)
        {
            mIsOnBlackLine = IsOnBlackLine;
        }
        else if (!IsOnBlackLine && mIsOnBlackLine)
        {
            mIsOnBlackLine = IsOnBlackLine;
            mScore -= ON_BLACk_LINE_PENALTY;
        }
    }

    private void InOpponentStationPenalty(bool IsInOpponentStation)
    {
        if (IsInOpponentStation)
        {
            mMileage -= IN_OPPONENT_STATION_PENALTY;
        }
    }

    private void InObstaclePenalty(bool IsInObstacle)
    {
        if (mIsInObstacle)
        {
            mMileage -= IN_OBSTACLE_PENALTY;
        }
    }
}