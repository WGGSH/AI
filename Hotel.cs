using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Ateam
{
    //かなり適当なAIなので強くしたい場合は修正
    public class Hotel : BaseBattleAISystem
    {
        struct HpData
        {
            public HpData(float oldHp, int actorId)
            {
                this.OldHp = oldHp;
                this.ActorId = actorId;
            }

            public float OldHp;
            public int ActorId;
        }

        readonly static float ATTACK_MIDDLE_THRESHOLD = 8;
        readonly static float ATTACK_SHORT_THRESHOLD = 2;
        readonly static float INVINCIBLE_THRESHOLD = 3;
        readonly static float MAIN_MOVE_UPDATE_THRESHOLD = 6;
        readonly static float SUB_MOVE_UPDATE_THRESHOLD = 3;

        CharacterModel.Data _mainCharacter = null;
        List<CharacterModel.Data> _subCharacterList = new List<CharacterModel.Data>();
        List<HpData> _oldHpList = new List<HpData>();
        List<Vector2> _mainMoveList = new List<Vector2>();

        List<Vector2> itemBlockPosList;


        //---------------------------------------------------
        // InitializeAI
        //---------------------------------------------------
        override public void InitializeAI()
        {
            //HPの情報初期化
            foreach (CharacterModel.Data data in GetTeamCharacterDataList(TEAM_TYPE.PLAYER))
            {
                _oldHpList.Add(new HpData(data.Hp, data.ActorId));
            }

            DataUpdate();

            this.itemBlockPosList = new List<Vector2>();
        }

        //---------------------------------------------------
        // UpdateAI
        //---------------------------------------------------
        override public void UpdateAI()
        {
            List<CharacterModel.Data> playerList = GetTeamCharacterDataList(TEAM_TYPE.PLAYER);
            List<CharacterModel.Data> enemyList = GetTeamCharacterDataList(TEAM_TYPE.ENEMY);

            //データの更新
            if (DataUpdate() == false)
            {
                return;
            }

            MoveUpdate();

            //攻撃更新
            ActionUpdate();
        }

        //---------------------------------------------------
        // MoveTargetBlock
        //---------------------------------------------------
        List<Vector2> MoveTargetBlock(int actorId, Vector2 targetBlockPos)
        {
            //ベクトル計算
            Vector2 currentBlockPos = GetCharacterData(actorId).BlockPos;
            Vector2 moveVec = targetBlockPos - GetCharacterData(actorId).BlockPos;

            //指定した地点まで障害物がない移動手段を構築
            List<Vector2> commandList = new List<Vector2>();      //実際の移動の軌跡
            List<Vector2> moveBlockList = new List<Vector2>();      //一度遠た箇所のメモ

            //開始位置を記憶
            Vector2 startBlock = currentBlockPos;
            moveBlockList.Add(startBlock);

            //移動方向ベクトル上下左右を定義
            List<Vector2> moveVecList = new List<Vector2>();
            moveVecList.Add(GetMoveTypeToVec(Common.MOVE_TYPE.UP));
            moveVecList.Add(GetMoveTypeToVec(Common.MOVE_TYPE.LEFT));
            moveVecList.Add(GetMoveTypeToVec(Common.MOVE_TYPE.RIGHT));
            moveVecList.Add(GetMoveTypeToVec(Common.MOVE_TYPE.DOWN));

            if (GetCharacterData(actorId).Hp <= 0)
            {
                return commandList;
            }

            while (targetBlockPos != currentBlockPos)
            {
                moveVec = targetBlockPos - currentBlockPos;

                //長い方を
                if (Mathf.Abs(moveVec.x) > Mathf.Abs(moveVec.y))
                {
                    moveVec.y = 0;
                }
                else
                {
                    moveVec.x = 0;
                }

                moveVec = moveVec.normalized;

                if (moveVec == Vector2.zero)
                {
                    break;
                }

                //基本は敵に向かうベクトルでいく
                Vector2 nextBlock = currentBlockPos + moveVec;

                //通れるブロッックの場合はそのまま
                if (GetBlockType(nextBlock) == Ateam.Define.Stage.BLOCK_TYPE.NORMAL
                    && Common.ExistsVector2List(moveBlockList, nextBlock) == false)
                {
                    //次のブロックへ
                    currentBlockPos = nextBlock;

                    commandList.Add(currentBlockPos);
                    moveBlockList.Add(currentBlockPos);
                }
                //通れないブロックか一度通った箇所ならこちら
                else
                {
                    bool flag = false;

                    //最短ベクトル計算
                    Vector2 vec = targetBlockPos - GetCharacterData(actorId).BlockPos;
                    vec.Normalize();
                    float angle = Vector2.Angle(moveVec, vec);

                    angle = (((angle / angle) * -1) * Common.DegToRad(90));

                    //                    if (float.IsNaN(angle))
                    {
                        angle = Common.DegToRad(90);
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 nextVec;
                        nextVec.x = moveVec.x * Mathf.Cos(angle) - moveVec.y * Mathf.Sin(angle);
                        nextVec.y = moveVec.x * Mathf.Sin(angle) + moveVec.y * Mathf.Cos(angle);

                        moveVec = nextVec;
                        nextBlock = currentBlockPos + moveVec;

                        if (GetBlockType(nextBlock) == Ateam.Define.Stage.BLOCK_TYPE.NORMAL
                            && Common.ExistsVector2List(moveBlockList, nextBlock) == false)
                        {
                            //次のブロックへ
                            currentBlockPos = nextBlock;

                            commandList.Add(currentBlockPos);
                            moveBlockList.Add(currentBlockPos);

                            flag = true;

                            break;
                        }
                    }

                    //上下左右すべて駄目だったので一個ブロックを戻る
                    if (flag == false)
                    {
                        if (commandList.Count > 0)
                        {
                            commandList.RemoveAt(commandList.Count - 1);
                        }
                        else
                        {
                            //上下左右だめな場合は閉じ込められている
                            commandList.Clear();
                            return commandList;
                        }

                        if (commandList.Count > 0)
                        {
                            currentBlockPos = commandList[commandList.Count - 1];
                        }
                        else
                        {
                            currentBlockPos = startBlock;
                        }
                    }
                }

            }

            return commandList;
        }

        //---------------------------------------------------
        // GetVecToMoveType
        //---------------------------------------------------
        public Common.MOVE_TYPE GetVecToMoveType(Vector2 vec)
        {
            if (vec == Vector2.down)
            {
                return Common.MOVE_TYPE.DOWN;
            }
            else if (vec == Vector2.up)
            {
                return Common.MOVE_TYPE.UP;
            }
            else if (vec == Vector2.right)
            {
                return Common.MOVE_TYPE.RIGHT;
            }
            else if (vec == Vector2.left)
            {
                return Common.MOVE_TYPE.LEFT;
            }

            return Common.MOVE_TYPE.NONE;
        }

        //---------------------------------------------------
        // GetVecToMoveType
        //---------------------------------------------------
        public Vector2 GetMoveTypeToVec(Common.MOVE_TYPE type)
        {
            Vector2 outVec = Vector2.zero;

            switch (type)
            {
                case Common.MOVE_TYPE.DOWN:
                    outVec = Vector2.down;
                    break;

                case Common.MOVE_TYPE.UP:
                    outVec = Vector2.up;
                    break;

                case Common.MOVE_TYPE.RIGHT:
                    outVec = Vector2.right;
                    break;

                case Common.MOVE_TYPE.LEFT:
                    outVec = Vector2.left;
                    break;
            }

            return outVec;
        }

        //---------------------------------------------------
        // MoveVec
        //---------------------------------------------------
        bool MoveVec(int actorId, Vector2 vec)
        {
            if (vec.x < 0)
            {
                return Move(actorId, Common.MOVE_TYPE.LEFT);
            }
            else if (vec.x > 0)
            {
                return Move(actorId, Common.MOVE_TYPE.RIGHT);
            }
            else if (vec.y < 0)
            {
                return Move(actorId, Common.MOVE_TYPE.DOWN);
            }
            else
            {
                return Move(actorId, Common.MOVE_TYPE.UP);
            }
        }

        //---------------------------------------------------
        // GetLowHpCharacterData
        //---------------------------------------------------
        CharacterModel.Data GetLowHpCharacterData(TEAM_TYPE type)
        {
            List<CharacterModel.Data> list = GetTeamCharacterDataList(type);
            CharacterModel.Data retData = null;
            float hp = 0;

            foreach (CharacterModel.Data data in list)
            {
                if (hp < data.Hp)
                {
                    retData = data;
                }
            }

            return retData;
        }

        //---------------------------------------------------
        // searchAliveTeam
        //---------------------------------------------------
        CharacterModel.Data searchAliveTeam(TEAM_TYPE type)
        {
            List<CharacterModel.Data> list = GetTeamCharacterDataList(type);
            CharacterModel.Data retData = null;

            foreach (CharacterModel.Data data in list)
            {
                if (data.Hp > 0)
                {
                    retData = data;
                    break;
                }
            }

            return retData;
        }

        //---------------------------------------------------
        // ActionUpdate
        //---------------------------------------------------
        void ActionUpdate()
        {
            List<CharacterModel.Data> playerList = GetTeamCharacterDataList(TEAM_TYPE.PLAYER);
            List<CharacterModel.Data> enemyList = GetTeamCharacterDataList(TEAM_TYPE.ENEMY);

            //遠距離は常に出し続ける
            foreach (CharacterModel.Data character in playerList)
            {
                Action(character.ActorId, Define.Battle.ACTION_TYPE.ATTACK_LONG);
            }

            //中距離と近距離は敵が一定範囲内にいたら発射
            //一定距離の場合バリア展開
            foreach (CharacterModel.Data playerData in playerList)
            {
                foreach (CharacterModel.Data enemyData in enemyList)
                {
                    if (enemyData.Hp <= 0)
                    {
                        continue;
                    }

                    float len = (enemyData.BlockPos - playerData.BlockPos).magnitude;

                    if (len < ATTACK_MIDDLE_THRESHOLD)
                    {
                        Action(playerData.ActorId, Define.Battle.ACTION_TYPE.ATTACK_MIDDLE);
                    }

                    if (len < ATTACK_SHORT_THRESHOLD)
                    {
                        Action(playerData.ActorId, Define.Battle.ACTION_TYPE.ATTACK_SHORT);
                    }

                    if (len < INVINCIBLE_THRESHOLD)
                    {
                        Action(playerData.ActorId, Define.Battle.ACTION_TYPE.INVINCIBLE);
                    }
                }
            }

        }

        //---------------------------------------------------
        // MoveUpdate
        //---------------------------------------------------
        void MoveUpdate()
        {
            //HPが弱いやつからターゲットにする
            CharacterModel.Data targetCharacterData = GetLowHpCharacterData(TEAM_TYPE.ENEMY);

            //Move(_mainCharacter.ActorId, Common.MOVE_TYPE.RIGHT);

            // アイテムがあればそこに移動
            if (this.itemBlockPosList.Count>=1)
            {
                _mainMoveList = MoveTargetBlock(_mainCharacter.ActorId, this.itemBlockPosList[0]);
            }
            
            //ターゲットの近くになったら更新
            if (targetCharacterData != null
                && (targetCharacterData.BlockPos - _mainCharacter.BlockPos).magnitude < MAIN_MOVE_UPDATE_THRESHOLD)
            {
                _mainMoveList = MoveTargetBlock(_mainCharacter.ActorId, targetCharacterData.BlockPos);
            }

            //旗艦だけ先に行動
            //残りのサブ戦闘機は旗艦の周りに配置して援護射撃
            if (targetCharacterData != null
                && _mainMoveList.Count <= 0)
            {
                //_mainMoveList = MoveTargetBlock(_mainCharacter.ActorId, targetCharacterData.BlockPos);
            }
            else if (_mainCharacter.isMoveEnable
                && _mainMoveList.Count > 0)
            {
                //経路探索されたもので移動
                Move(_mainCharacter.ActorId, GetVecToMoveType(_mainMoveList[0] - _mainCharacter.BlockPos));
                _mainMoveList.RemoveAt(0);
            }

            //サブ機はメイン機の周りに配置
            //一定の距離になったらターゲットに突撃
            //周りに空きがなかったら待機
            foreach (CharacterModel.Data data in _subCharacterList)
            {
                //ターゲットの近くになったらそっちに移動
                if (targetCharacterData != null
                   && (targetCharacterData.BlockPos - data.BlockPos).magnitude < SUB_MOVE_UPDATE_THRESHOLD)
                {
                    List<Vector2> moveList = MoveTargetBlock(data.ActorId, targetCharacterData.BlockPos);

                    if (moveList.Count > 0)
                    {
                        Move(data.ActorId, GetVecToMoveType(moveList[0] - data.BlockPos));
                    }
                }
                else
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 block;
                        int bias = UnityEngine.Random.Range(0, 2) - 1;
                        block.x = _mainCharacter.BlockPos.x + (UnityEngine.Random.Range(2, 2) * bias);
                        block.y = _mainCharacter.BlockPos.y + (UnityEngine.Random.Range(2, 2) * bias);

                        if (GetBlockType(block) == Ateam.Define.Stage.BLOCK_TYPE.NORMAL)
                        {
                            List<Vector2> moveList = MoveTargetBlock(data.ActorId, block);

                            if (moveList.Count != 0)
                            {
                                Move(data.ActorId, GetVecToMoveType(moveList[0] - data.BlockPos));
                            }

                            break;
                        }
                    }
                }
            }

            // アイテムの場所にたどり着いていれば
            foreach(Vector2 itemPos in this.itemBlockPosList){
                if (_mainCharacter.BlockPos == itemPos)
                {
                    this.itemBlockPosList.Remove(itemPos);
                }
            }
        }

        //---------------------------------------------------
        // DataUpdate
        //---------------------------------------------------
        bool DataUpdate()
        {
            List<CharacterModel.Data> playerList = GetTeamCharacterDataList(TEAM_TYPE.PLAYER);
            _subCharacterList.Clear();

            //生存している戦闘機を旗艦に設定
            _mainCharacter = searchAliveTeam(TEAM_TYPE.PLAYER);

            if (_mainCharacter == null)
            {
                return false;
            }

            //サブ機を更新
            foreach (CharacterModel.Data data in playerList)
            {
                if (_mainCharacter.Equals(data) || data.Hp <= 0)
                {
                    continue;
                }

                _subCharacterList.Add(data);
            }

            for (int i = 0; i < _oldHpList.Count; i++)
            {
                HpData hpData = _oldHpList[i];

                foreach (CharacterModel.Data playerData in playerList)
                {
                    if (hpData.ActorId == playerData.ActorId)
                    {
                        if (hpData.OldHp != playerData.Hp)
                        {
                            DamageCallBack(playerData.ActorId);
                        }

                        hpData.OldHp = playerData.Hp;
                    }
                }
            }


            return true;
        }

        //---------------------------------------------------
        // DamageCallBack
        //---------------------------------------------------
        void DamageCallBack(int actorId)
        {
            if (GetCharacterData(actorId).Hp < 300)
            {
                Action(actorId, Define.Battle.ACTION_TYPE.INVINCIBLE);
            }
        }

        //---------------------------------------------------
        // ItemSpawnCallback
        // アイテムが発生したときに呼び出される
        //---------------------------------------------------
        override public void ItemSpawnCallback(ItemSpawnData itemData)
        {
            /*
             *  ATTACK_UP, 
             *  SPEED_UP,
             *  HP_RECOVER, 
            */
            this.itemBlockPosList.Add(itemData.BlockPos);
        }
    }
}
