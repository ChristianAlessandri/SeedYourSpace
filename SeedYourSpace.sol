// SPDX-License-Identifier: MIT
pragma solidity 0.8.34;

// OpenZeppelin Imports (v5)
import {ERC721} from "@openzeppelin/contracts/token/ERC721/ERC721.sol";
import {ERC721Enumerable} from "@openzeppelin/contracts/token/ERC721/extensions/ERC721Enumerable.sol";
import {Strings} from "@openzeppelin/contracts/utils/Strings.sol";
import {Base64} from "@openzeppelin/contracts/utils/Base64.sol";

// Chainlink VRF v2.5 Imports
import {VRFConsumerBaseV2Plus} from "@chainlink/contracts/src/v0.8/vrf/dev/VRFConsumerBaseV2Plus.sol";
import {VRFV2PlusClient} from "@chainlink/contracts/src/v0.8/vrf/dev/libraries/VRFV2PlusClient.sol";

/**
 * @title SeedYourSpace
 * @dev Procedural Solar System NFT generator using Chainlink VRF.
 * Implements a highly secure asynchronous Request-Fulfill-Claim pattern.
 */
contract SeedYourSpace is ERC721Enumerable, VRFConsumerBaseV2Plus {
    using Strings for uint256;

    // --- CUSTOM ERRORS (Gas Optimization) ---
    error ExactMintPriceRequired(uint256 sent, uint256 required);
    error TooManyGlobalPendingRequests();
    error UserAlreadyHasPendingRequest();
    error RequestNotFound();
    error RandomnessNotFulfilled();
    error NotRequestOwner();
    error NoBalanceToWithdraw();
    error WithdrawalFailed();

    // --- STRUCTS & MAPPINGS ---
    struct SystemData {
        uint256 seed;
        uint256 algorithmVersion;
    }

    // Unified struct to snapshot data and track fulfillment state
    struct PendingRequest {
        address minter;
        uint256 algorithmVersionSnapshot;
        uint256 generatedSeed;
        bool isFulfilled;
    }

    mapping(uint256 => SystemData) public systems;
    mapping(uint256 => PendingRequest) public vrfRequests;
    
    // Per-user limit mapping to prevent Sybil DoS
    mapping(address => uint256) public pendingRequestsByUser;

    // --- STATE VARIABLES ---
    uint256 public nextTokenId = 1;
    uint256 public algorithmVersion = 1;
    uint256 public mintPrice = 0.001 ether; 
    
    uint256 public activeGlobalRequests = 0;
    uint256 public maxPendingRequests = 100;
    uint256 public constant MAX_PENDING_PER_USER = 1;

    // --- CHAINLINK VRF VARIABLES ---
    uint256 public s_subscriptionId;
    bytes32 public constant SEPOLIA_KEY_HASH = 0x787d74caea10b2b357790d5b5247c2f63d1d91572a9846f780606e4d953677ae; 
    uint32 public callbackGasLimit = 400000; 
    uint16 public constant REQUEST_CONFIRMATIONS = 3;
    uint32 public constant NUM_WORDS = 1;

    // --- EVENTS ---
    event SystemRequested(uint256 indexed requestId, address indexed requester, uint256 algorithmVersion);
    event RandomnessArrived(uint256 indexed requestId, uint256 seed);
    event SystemClaimed(uint256 indexed requestId, uint256 indexed tokenId, address indexed owner, uint256 seed);
    
    event MintPriceUpdated(uint256 oldPrice, uint256 newPrice);
    event AlgorithmVersionUpdated(uint256 oldVersion, uint256 newVersion);
    event MaxPendingRequestsUpdated(uint256 oldMax, uint256 newMax);

    /**
     * @dev Constructor
     * @param _subscriptionId The Chainlink VRF v2.5 subscription ID
     */
    constructor(uint256 _subscriptionId) 
        ERC721("Seed Your Space", "SYS")
        VRFConsumerBaseV2Plus(0x9DdfaCa8183c41ad55329BdeeD9F6A8d53168B1B) 
    {
        s_subscriptionId = _subscriptionId;
    }

    // --- CORE LOGIC (REQUEST -> FULFILL -> CLAIM) ---

    /**
     * @notice Step 1: User requests randomness. 
     */
    function requestSystemMint() external payable returns (uint256 requestId) {
        if (msg.value != mintPrice) revert ExactMintPriceRequired(msg.value, mintPrice);
        if (activeGlobalRequests >= maxPendingRequests) revert TooManyGlobalPendingRequests();
        if (pendingRequestsByUser[msg.sender] >= MAX_PENDING_PER_USER) revert UserAlreadyHasPendingRequest();

        requestId = s_vrfCoordinator.requestRandomWords(
            VRFV2PlusClient.RandomWordsRequest({
                keyHash: SEPOLIA_KEY_HASH,
                subId: s_subscriptionId,
                requestConfirmations: REQUEST_CONFIRMATIONS,
                callbackGasLimit: callbackGasLimit,
                numWords: NUM_WORDS,
                extraArgs: VRFV2PlusClient._argsToBytes(
                    VRFV2PlusClient.ExtraArgsV1({nativePayment: false})
                )
            })
        );

        // Snapshot state to guarantee determinism
        vrfRequests[requestId] = PendingRequest({
            minter: msg.sender,
            algorithmVersionSnapshot: algorithmVersion,
            generatedSeed: 0,
            isFulfilled: false
        });

        pendingRequestsByUser[msg.sender]++;
        activeGlobalRequests++;

        emit SystemRequested(requestId, msg.sender, algorithmVersion);
    }

    /**
     * @notice Step 2: Chainlink securely returns the seed. We ONLY store it to prevent callback reverts.
     */
    function fulfillRandomWords(uint256 requestId, uint256[] calldata randomWords) internal override {
        PendingRequest storage req = vrfRequests[requestId];
        
        // If request is invalid, silently return to not revert Chainlink's callback
        if (req.minter == address(0)) return;

        req.generatedSeed = randomWords[0];
        req.isFulfilled = true;

        emit RandomnessArrived(requestId, randomWords[0]);
    }

    /**
     * @notice Step 3: User claims the NFT safely. Uses _safeMint securely.
     */
    function claimSystem(uint256 requestId) external {
        PendingRequest memory req = vrfRequests[requestId];
        
        if (req.minter == address(0)) revert RequestNotFound();
        if (req.minter != msg.sender) revert NotRequestOwner();
        if (!req.isFulfilled) revert RandomnessNotFulfilled();

        uint256 tokenId = nextTokenId++;
        uint256 seed = req.generatedSeed;

        systems[tokenId] = SystemData({
            seed: seed,
            algorithmVersion: req.algorithmVersionSnapshot
        });

        // State cleanup before external call (Checks-Effects-Interactions pattern)
        delete vrfRequests[requestId];
        pendingRequestsByUser[msg.sender]--;
        activeGlobalRequests--;

        _safeMint(msg.sender, tokenId);
        
        emit SystemClaimed(requestId, tokenId, msg.sender, seed);
    }

    // --- ON-CHAIN SVG GENERATION ---

    function _generateColor(uint256 _seed, uint256 _shift) internal pure returns (string memory) {
        uint24 colorNumber = uint24((_seed >> _shift) & 0xFFFFFF);
        string memory hexString = Strings.toHexString(uint256(colorNumber), 3);
        
        bytes memory hexBytes = bytes(hexString);
        bytes memory colorBytes = new bytes(7);
        colorBytes[0] = 0x23; 
        for (uint i = 0; i < 6; i++) {
            colorBytes[i + 1] = hexBytes[i + 2];
        }
        
        return string(colorBytes);
    }

    /**
     * @dev Generates a 5x5 pixel-art style grid. 
     * Hashes the VRF seed to decorrelate the visual output, 
     * then deterministically derives palette colors and grid cells.
     */
    function _generateSVG(uint256 _seed) internal pure returns (string memory) {
        uint256 fullSeed = uint256(keccak256(abi.encodePacked(_seed)));

        string[4] memory palette = [
            _generateColor(fullSeed, 0),
            _generateColor(fullSeed, 24),
            _generateColor(fullSeed, 48),
            _generateColor(fullSeed, 72)
        ];

        string memory svgBody = "";

        for (uint256 i = 0; i < 25; i++) {
            uint256 colorIndex = (fullSeed >> (100 + (i * 2))) & 3;
            uint256 x = (i % 5) * 20;
            uint256 y = (i / 5) * 20;

            svgBody = string(
                abi.encodePacked(
                    svgBody,
                    '<rect x="', x.toString(), '" y="', y.toString(), '" width="20" height="20" fill="', palette[colorIndex], '"/>'
                )
            );
        }

        return string(
            abi.encodePacked(
                '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="400" height="400">',
                svgBody,
                '</svg>'
            )
        );
    }

    function previewSVG(uint256 _testSeed) external pure returns (string memory) {
        return _generateSVG(_testSeed);
    }

    // --- METADATA (ON-CHAIN JSON) ---

    function tokenURI(uint256 tokenId) public view override returns (string memory) {
        _requireOwned(tokenId);

        SystemData memory data = systems[tokenId];
        string memory svg = _generateSVG(data.seed);

        string memory base64Svg = string(
            abi.encodePacked(
                "data:image/svg+xml;base64,",
                Base64.encode(bytes(svg))
            )
        );

        string memory json = string(
            abi.encodePacked(
                '{"name": "SYStem #', tokenId.toString(), '", ',
                '"description": "Procedural stellar coordinates rendered in Unity.", ',
                '"image": "', base64Svg, '", ',
                '"attributes": [',
                    '{"trait_type": "Seed", "display_type": "number", "value": ', data.seed.toString(), '}, ',
                    '{"trait_type": "Algorithm Version", "value": "', data.algorithmVersion.toString(), '"}',
                ']}'
            )
        );

        return string(
            abi.encodePacked(
                "data:application/json;base64,",
                Base64.encode(bytes(json))
            )
        );
    }

    // --- ADMIN FUNCTIONS ---

    function setAlgorithmVersion(uint256 _newVersion) external onlyOwner {
        uint256 oldVersion = algorithmVersion;
        algorithmVersion = _newVersion;
        emit AlgorithmVersionUpdated(oldVersion, _newVersion);
    }

    function setMintPrice(uint256 _newPrice) external onlyOwner {
        uint256 oldPrice = mintPrice;
        mintPrice = _newPrice;
        emit MintPriceUpdated(oldPrice, _newPrice);
    }

    // Added to prevent global queue locking if users never claim their NFTs
    function setMaxPendingRequests(uint256 _newMax) external onlyOwner {
        uint256 oldMax = maxPendingRequests;
        maxPendingRequests = _newMax;
        emit MaxPendingRequestsUpdated(oldMax, _newMax);
    }

    function withdraw() external onlyOwner {
        uint256 balance = address(this).balance;
        if (balance == 0) revert NoBalanceToWithdraw();
        
        (bool success, ) = owner().call{value: balance}("");
        if (!success) revert WithdrawalFailed();
    }
}