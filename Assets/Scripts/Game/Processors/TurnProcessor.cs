using System;
using System.Collections.Generic;

public class TurnProcessor
{
    private TileQueue tileQueue;
    private TilesContainer discardedTilesContainer;
    private RequestQueue requestQueue;
    private Player player0; // Bottom
    private Player opponent1; // Right
    private Player opponent2; // Top
    private Player opponent3; // Left
    private Player[] players;
    public void NewGame()
    {
        tileQueue = new TileQueue();
        tileQueue.Randomize();
        discardedTilesContainer = new TilesContainer();
        requestQueue = new RequestQueue();
        this.player0 = PlayerUtils.GetPlayer0();
        this.opponent1 = PlayerUtils.GetOpponent1();
        this.opponent2 = PlayerUtils.GetOpponent2();
        this.opponent3 = PlayerUtils.GetOpponent3();
        this.players = new Player[] { player0, opponent1, opponent2, opponent3 };
        player0.DrawStartingTiles(tileQueue);
        opponent1.DrawStartingTiles(tileQueue);
        opponent2.DrawStartingTiles(tileQueue);
        opponent3.DrawStartingTiles(tileQueue);
        SetPlayerWinds(player0, Winds.EAST);
        RefreshAllPlayersTilesDisplay();
    }
    public void DrawPlayer0Tile()
    {
        DrawTile(player0);
    }
    public void DiscardPlayer0Tile(int index)
    {
        DiscardTile(index, player0);
    }
    public void ExecutePlayer0TileAction(TileAction tileAction)
    {
        ExecuteTileAction(tileAction, player0);
    }
    public void ProcessPlayer0TileActionRequest(TileAction tileAction)
    {
        ProcessTileActionRequest(tileAction, player0);
    }
    public TilesContainer GetPlayer0MainTiles()
    {
        return this.player0.GetMainTiles();
    }
    public TilesContainer GetPlayer0FlowerTiles()
    {
        return this.player0.GetFlowerTiles();
    }
    public TilesContainer GetOpponent1MainTiles()
    {
        return this.opponent1.GetMainTiles();
    }
    public TilesContainer GetOpponent1FlowerTiles()
    {
        return this.opponent1.GetFlowerTiles();
    }
    public TilesContainer GetOpponent2MainTiles()
    {
        return this.opponent2.GetMainTiles();
    }
    public TilesContainer GetOpponent2FlowerTiles()
    {
        return this.opponent2.GetFlowerTiles();
    }
    public TilesContainer GetOpponent3MainTiles()
    {
        return this.opponent3.GetMainTiles();
    }
    public TilesContainer GetOpponent3FlowerTiles()
    {
        return this.opponent3.GetFlowerTiles();
    }
    public TilesContainer GetDiscardedTiles()
    {
        return this.discardedTilesContainer;
    }
    public void AutoPlayDraw()
    {
        Player player = players[MapperUtils.MapGameStateToPlayerId(GameStateController.instance.gameState)];
        GameStateController.instance.gameState = GameStates.PROCESSING; // Can only set gameState after mapping gameState to player
        DrawTile(player);
    }
    public void AutoPlayDiscard()
    {
        Player player = players[MapperUtils.MapGameStateToPlayerId(GameStateController.instance.gameState)];
        DiscardTile(0, player);
    }
    private void SetPlayerWinds(Player player, Winds wind)
    {
        for (int i = 0; i < 4; i++)
        {
            if (wind == Winds.EAST)
            {
                GameStateController.instance.gameState = (GameStates)player.GetId();
            }
            player.SetWind(wind);
            player = GetNextPlayer(player);
            wind = wind.Next<Winds>();
        }
    }
    private void DrawTile(Player drawingPlayer)
    {
        bool isPlayer0Drawing = drawingPlayer.GetId() == PlayerUtils.PLAYER0_ID;
        drawingPlayer.DrawTile(tileQueue);
        if (isPlayer0Drawing)
        {
            GameStateController.instance.DisplayTileActions(drawingPlayer.GetPossibleTileActionsFromDrawnTile());
            GameStateController.instance.StartDiscardTimerCoroutine();
        }
        RefreshAllPlayersTilesDisplay(false, isPlayer0Drawing);
        GameStateController.instance.gameState = MapperUtils.MapPlayerIdToDiscardingGameState(drawingPlayer.GetId());
    }
    private void DiscardTile(int tileIndex, Player discardingPlayer)
    {
        GameStateController.instance.gameState = GameStates.PROCESSING;
        discardingPlayer.DiscardTile(tileIndex, discardedTilesContainer);
        discardingPlayer.SortTiles();
        RefreshAllPlayersTilesDisplay();
        Tile discardedTile = discardedTilesContainer.GetLastTile();
        Offer(discardedTile, discardingPlayer);
    }
    private void ExecuteRequest(Request request)
    {
        ExecuteTileAction(request.GetTileAction(), request.GetRequestingPlayer(), true);
    }
    private void ExecuteTileAction(TileAction tileAction, Player executingPlayer, bool isFromOffer = false)
    {
        executingPlayer.ExecuteTileAction(tileAction, isFromOffer);
        RefreshAllPlayersTilesDisplay();
        switch (tileAction.GetTileActionType())
        {
            case TileActionTypes.KONG:
                DrawTile(executingPlayer);
                break;
            case TileActionTypes.CHOW:
            case TileActionTypes.PONG:
                GameStateController.instance.gameState = MapperUtils.MapPlayerIdToDiscardingGameState(executingPlayer.GetId());
                if (executingPlayer.GetId() == PlayerUtils.PLAYER0_ID)
                {
                    GameStateController.instance.StartDiscardTimerCoroutine();
                }
                break;
            case TileActionTypes.HU:
                GameStateController.instance.gameState = GameStates.PROCESSING;
                GameStateController.instance.StartHuTimerCoroutine();
                break;
            default:
                break;
        }
    }
    private void ProcessTileActionRequest(TileAction tileAction, Player requestingPlayer)
    {
        requestQueue.Add(new Request(tileAction, requestingPlayer));
        if (requestQueue.IsFull())
        {
            Request request = requestQueue.GetHighestPriorityRequest();
            requestQueue.Reset();
            if (request.IsEmpty())
            {
                int currentTurnPlayerId = MapperUtils.MapGameStateToPlayerId(GameStateController.instance.gameState);
                Player nextTurnPlayer = GetNextPlayer(currentTurnPlayerId);
                GameStateController.instance.gameState = MapperUtils.MapPlayerIdToDrawingGameState(nextTurnPlayer.GetId());
            }
            else
            {
                discardedTilesContainer.RemoveLastTile();
                RefreshAllPlayersTilesDisplay();
                ExecuteRequest(request);
            }
        }
    }
    private void Offer(Tile discardedTile, Player offeringPlayer)
    {
        GameStateController.instance.gameState = MapperUtils.MapPlayerIdToOfferingGameState(offeringPlayer.GetId());
        requestQueue.SetOfferingPlayer(offeringPlayer);
        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == offeringPlayer)
            {
                continue;
            }
            if (player.GetId() == PlayerUtils.PLAYER0_ID)
            {
                List<TileAction> tileActions = player.GetPossibleTileActionsFromOfferedTile(discardedTile, offeringPlayer);
                if (tileActions.Count == 0)
                {
                    ProcessTileActionRequest(null, player);
                }
                else
                {
                    GameStateController.instance.DisplayTileActions(tileActions);
                    GameStateController.instance.StartOfferTimerCoroutine();
                }
                continue;
            }
            else
            {
                ProcessTileActionRequest(null, player);
            }
        }
    }
    private Player GetNextPlayer(Player currentPlayer)
    {
        int currentPlayerIndex = Array.IndexOf(players, currentPlayer);
        return GetNextPlayer(currentPlayerIndex);
    }
    private Player GetNextPlayer(int currentPlayerIndex)
    {
        int nextPlayerIndex = currentPlayerIndex + 1;
        if (nextPlayerIndex >= players.Length)
        {
            nextPlayerIndex = 0;
        }
        return players[nextPlayerIndex];
    }
    private void RefreshAllPlayersTilesDisplay(bool showOpponentContent = false, bool isAfterDrawingTile = false)
    {
        GameStateController.instance.RefreshPlayer0TilesDisplays(isAfterDrawingTile);
        GameStateController.instance.RefreshOpponent1TilesDisplay(showOpponentContent);
        GameStateController.instance.RefreshOpponent2TilesDisplay(showOpponentContent);
        GameStateController.instance.RefreshOpponent3TilesDisplay(showOpponentContent);
        GameStateController.instance.RefreshDiscardedTilesDisplays();
    }
}