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
        List<List<Vector2>> _subMoveList = new List<List<Vector2>>();

        Vector2 mainTargetPos;
        List<Vector2> subTargetPosList = new List<Vector2>();

        List<ItemSpawnData> itemList = new List<ItemSpawnData>();

        List<CharacterModel.Data> playerList;
        List<CharacterModel.Data> enemyList;
        int ct;

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

            this.itemList = new List<ItemSpawnData>();

        }

        //---------------------------------------------------
        // UpdateAI
        //---------------------------------------------------
        override public void UpdateAI()
        {
            /*List<CharacterModel.Data> playerList = GetTeamCharacterDataList(TEAM_TYPE.PLAYER);
            List<CharacterModel.Data> enemyList = GetTeamCharacterDataList(TEAM_TYPE.ENEMY);*/
            this.playerList = GetTeamCharacterDataList(TEAM_TYPE.PLAYER);
            this.enemyList = GetTeamCharacterDataList(TEAM_TYPE.ENEMY);

            //データの更新
            if (DataUpdate() == false)
            {
                return;
            }

            if (ct % 5 == 0)
            {
                MoveUpdate();
            }
            ct++;

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
            /*if (this.itemList.Count >= 1)
            {
                // 最も近いアイテムを探す
                int minIndex = -1;
                float minDist = 99999;
                for (int i = 0; i < this.itemList.Count; i++)
                {
                    float length = Vector2.Distance(this._mainCharacter.BlockPos, itemList[i].BlockPos);
                    if (length < minDist)
                    {
                        minDist = length;
                        minIndex = i;
                    }
                }
                this.mainTargetPos = this.itemList[minIndex].BlockPos;
            }
            else
            {
                if (targetCharacterData != null)
                {
                    this.mainTargetPos = targetCharacterData.BlockPos;
                }
            }*/

            this.selectTarget();


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

                _mainMoveList = MoveTargetBlock(_mainCharacter.ActorId, this.mainTargetPos);

            // サブ機は，親機の少し後ろで待機
            // ただし，HPが減っていて，回復アイテムが存在すればそれを取りに行くことを優先する
            foreach (CharacterModel.Data data in _subCharacterList)
            {
                if (data.Hp < 300)
                {
                    ItemSpawnData hpItem = this.getItem(ItemData.ITEM_TYPE.HP_RECOVER);
                    //_subMoveList[0] = MoveTargetBlock(_subCharacterList[0].ActorId, this.mainTargetPos - subTargetVec);

                    List<Vector2> moveList = MoveTargetBlock(data.ActorId, this._mainCharacter.BlockPos - hpItem.BlockPos);
                    if (moveList.Count > 0)
                    {
                        Move(data.ActorId, GetVecToMoveType(moveList[0] - data.BlockPos));
                    }
                }
            }

            Vector2 subTargetVec = this.mainTargetPos - this._mainCharacter.BlockPos;
            if (Mathf.Abs(subTargetVec.x) > Mathf.Abs(subTargetVec.y))
            {
                subTargetVec.y = 0;
            }
            else
            {
                subTargetVec.x = 0;
            }
            subTargetVec = subTargetVec.normalized;
            //_subMoveList[0] = MoveTargetBlock(_subCharacterList[0].ActorId, this.mainTargetPos - subTargetVec);
            foreach (CharacterModel.Data data in _subCharacterList)
            {
                List<Vector2> moveList = MoveTargetBlock(data.ActorId, this._mainCharacter.BlockPos - subTargetVec * 2);
                if (moveList.Count > 0)
                {
                    Move(data.ActorId, GetVecToMoveType(moveList[0] - data.BlockPos));
                }
            }



            // アイテムの場所にたどり着いていれば
            int count = -1;
            foreach (CharacterModel.Data player in this.playerList)
            {
                for (int i = 0; i < itemList.Count; i++) {
                    if (player.BlockPos == itemList[i].BlockPos)
                    {
                        count = i;
                    }
                }
            }
            foreach (CharacterModel.Data enemy in this.enemyList)
            {
                for (int i = 0; i < itemList.Count; i++)
                {
                    if (enemy.BlockPos == itemList[i].BlockPos)
                    {
                        count = i;
                    }
                }
            }
            if (count != -1)
            {
                this.itemList.Remove(this.itemList[count]);
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
            //_mainCharacter = searchAliveTeam(TEAM_TYPE.PLAYER);
            _mainCharacter = GetLowHpCharacterData(TEAM_TYPE.PLAYER);

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
            //Action(actorId, Define.Battle.ACTION_TYPE.INVINCIBLE);
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
            this.itemList.Add(itemData);
        }

        struct Score
        {
            public float value;
            public Vector2 blockPos;
        }

        void selectTarget()
        {
            //HPが弱いやつからターゲットにする
            CharacterModel.Data targetCharacterData = GetLowHpCharacterData(TEAM_TYPE.ENEMY);

            List<Score> scoreList = new List<Score>();

            if (targetCharacterData == null) return;
            Score scoreEnemy;
            scoreEnemy.value= Vector2.Distance(this._mainCharacter.BlockPos, targetCharacterData.BlockPos) * 20;
            scoreEnemy.blockPos = targetCharacterData.BlockPos;
            scoreList.Add(scoreEnemy);
            foreach (ItemSpawnData item in this.itemList)
            {
                float typevalue = 1;
                if (item.ItemType == ItemData.ITEM_TYPE.ATTACK_UP) typevalue = 50;
                if (item.ItemType == ItemData.ITEM_TYPE.SPEED_UP) typevalue = 100;
                if (item.ItemType == ItemData.ITEM_TYPE.HP_RECOVER) typevalue = 0;
                Score score = new Score();
                score.value = Vector2.Distance(item.BlockPos, this._mainCharacter.BlockPos) * typevalue;
                score.blockPos = item.BlockPos;
                scoreList.Add(score);
            }

            // 最もハイスコアのターゲットに移動
            float minScore = 99999999;
            int minIndex=-1;
            for (int i = 0; i < scoreList.Count; i++) { 
                Debug.Log(scoreList[i].blockPos+","+scoreList[i].value);
                if (scoreList[i].value < minScore)
                {
                    minScore = scoreList[i].value;
                    minIndex = i;
                }
            }
            this.mainTargetPos = scoreList[minIndex].blockPos ;


            /*// アイテムがあればそこに移動
            if (this.itemList.Count >= 1)
            {
                // 最も近いアイテムを探す
                int minIndex = -1;
                float minDist = 99999;
                for (int i = 0; i < this.itemList.Count; i++)
                {
                    float length = Vector2.Distance(this._mainCharacter.BlockPos, itemList[i].BlockPos);
                    if (length < minDist)
                    {
                        minDist = length;
                        minIndex = i;
                    }
                }
                this.mainTargetPos = this.itemList[minIndex].BlockPos;
            }
            else
            {
                if (targetCharacterData != null)
                {
                    this.mainTargetPos = targetCharacterData.BlockPos;
                }
            }*/
        }

        bool isExistItem(ItemData.ITEM_TYPE type)
        {
            foreach (ItemSpawnData item in this.itemList)
            {
                if (item.ItemType == type)
                {
                    return true;
                }
            }
            return false;
        }

        // 指定のタイプのアイテムを取得する
        ItemSpawnData getItem(ItemData.ITEM_TYPE type)
        {
            float minDist=99999;
            ItemSpawnData returnItem = new ItemSpawnData(); 
            foreach (ItemSpawnData item in this.itemList)
            {
                if (item.ItemType == type)
                {
                    float dist = Vector2.Distance(this._mainCharacter.BlockPos, item.BlockPos);
                    if (dist < minDist) returnItem = item;
                }
            }
            return returnItem;
        }
    }
}
